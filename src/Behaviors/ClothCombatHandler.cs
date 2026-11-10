using ClothTheWarriorCompanion.Config;

namespace ClothTheWarriorCompanion.Behaviors;

/// <summary>
/// Gestiona la detección, validación y combate con enemigos, así como el daño por impacto (hitbox de ataque) de Cloth.
/// </summary>
internal sealed class ClothCombatHandler {
    #region Tuning & Configuration

    private static ClothSettings Settings => ClothSettings.Current;

    public static int DefaultSlamDamageAmount => Settings.SlamDamageAmount;
    public static float AttackRange => Settings.AttackRange;
    public static float AttackMaxSpeedX => Settings.AttackMaxSpeedX;
    public static float DiveGravityMultiplier => Settings.DiveGravityMultiplier;
    public static float AttackAnimDistance => Settings.AttackAnimDistance;
    public static float AirHitDistance => Settings.AirHitDistance;
    public static float DetectionRadiusCloth => Settings.DetectionRadiusCloth;
    public static float ProtectionRadiusHero => Settings.ProtectionRadiusHero;

    // Ataque corto
    public static float CloseAttackDistance => Settings.CloseAttackDistance;
    public static float CloseAttackMaxDeltaY => Settings.CloseAttackMaxDeltaY;
    public static float CloseJumpHeight => Settings.CloseJumpHeight;
    public static float CloseApexTime => Settings.CloseApexTime;
    public static float CloseAttackSpeedX => Settings.CloseAttackSpeedX;
    public static float CloseDiveGravityMultiplier => Settings.CloseDiveGravityMultiplier;
    public static float CloseAnticDuration => Settings.CloseAnticDuration;
    public static float CloseAnimLeadTime => Settings.CloseAnimLeadTime;
    public static float CloseAnimDistance => Settings.CloseAnimDistance;
    public static float CloseAirHitDistance => Settings.CloseAirHitDistance;
    public static float CloseLandCheckDelay => Settings.CloseLandCheckDelay;
    public static float CloseRecoverDurationScale => Settings.CloseRecoverDurationScale;
    public static float CloseAttackCooldown => Settings.CloseAttackCooldown;

    // Animación / timing del ataque normal
    public static float AttackAnticDuration => Settings.AttackAnticDuration;
    public static float AttackAnimLeadTime => Settings.AttackAnimLeadTime;
    public static float AttackLandCheckDelay => Settings.AttackLandCheckDelay;
    public static float RecoverDurationScale => Settings.RecoverDurationScale;

    // Caída tras el golpe (ambos ataques)
    public static float PostAttackFallDelay => Settings.PostAttackFallDelay;
    public static int PostAttackJumpFrames => Settings.PostAttackJumpFrames;
    public static int PostAttackFallRecoverStartFrame => Settings.PostAttackFallRecoverStartFrame;

    // Salto de retroceso (sin golpe)
    public static float RetreatTriggerDistance => Settings.RetreatTriggerDistance;
    public static float RetreatMaxDeltaY => Settings.RetreatMaxDeltaY;
    public static float RetreatDistance => Settings.RetreatDistance;
    public static float RetreatJumpHeight => Settings.RetreatJumpHeight;
    public static float RetreatApexTime => Settings.RetreatApexTime;
    public static float RetreatSpeedX => Settings.RetreatSpeedX;
    public static float RetreatDiveGravityMultiplier => Settings.RetreatDiveGravityMultiplier;
    public static float RetreatAnticDuration => Settings.RetreatAnticDuration;
    public static float RetreatLandCheckDelay => Settings.RetreatLandCheckDelay;
    public static float RetreatRecoverDurationScale => Settings.RetreatRecoverDurationScale;
    public static float RetreatCooldown => Settings.RetreatCooldown;

    // Pruebas / duelo
    public static bool AttackPlayerEnabled => Settings.AttackPlayer;

    /// <summary>
    /// Se dispara cuando la hitbox de Cloth conecta con el Caballero (solo si AttackPlayer está activo).
    /// Punto de extensión para el modo duelo: suscribirse aquí y aplicar daño real al héroe.
    /// Parámetros: origen del golpe, centro de la hitbox, daño configurado.
    /// </summary>
    public static event Action<GameObject, Vector2, int>? PlayerHit;

    public static bool IsPlayer(GameObject? target) {
        var hero = HeroController.instance;
        return target != null && hero != null && target == hero.gameObject;
    }

    // Hitbox de ataque
    public static Vector2 AttackBoxSize =>
        new(Mathf.Max(0.1f, Settings.AttackBoxWidth), Mathf.Max(0.1f, Settings.AttackBoxHeight));

    /// <summary>
    /// Orientación de Cloth: 1 = derecha, -1 = izquierda (según el signo de localScale.x).
    /// </summary>
    public static float GetFacing(Transform t) => t.localScale.x < 0f ? -1f : 1f;

    /// <summary>
    /// Centro de la hitbox de ataque en coordenadas de mundo, invertida según la orientación.
    /// </summary>
    public static Vector2 GetAttackBoxCenter(Vector2 origin, float facing) =>
        origin + new Vector2(Settings.AttackBoxOffsetX * (facing < 0f ? -1f : 1f), Settings.AttackBoxOffsetY);

    #endregion

    private static readonly Collider2D[] OverlapBuffer = new Collider2D[64];
    private static readonly HashSet<HealthManager> DamagedTargetsBuffer = new();
    private static readonly List<HealthManager> TargetsBuffer = new();

    public bool IsValidTarget(GameObject? target, GameObject selfObject) {
        if (target == null || target == selfObject) return false;
        if (IsPlayer(target)) return AttackPlayerEnabled && target.activeInHierarchy;
        var healthManager = target.GetComponent<HealthManager>();
        return healthManager != null && healthManager.isActiveAndEnabled && !healthManager.isDead && !healthManager.IsInvincible && healthManager.hp > 0;
    }

    public GameObject? FindBestEnemy(Vector2 clothCenter, Vector2 heroCenter, GameObject selfObject) {
        // Modo prueba/duelo: el Caballero es el objetivo prioritario si está dentro del radio de detección
        if (AttackPlayerEnabled) {
            var hero = HeroController.instance;
            if (hero != null
                && IsValidTarget(hero.gameObject, selfObject)
                && Vector2.Distance(clothCenter, heroCenter) <= DetectionRadiusCloth) {
                return hero.gameObject;
            }
        }

        GameObject? closestEnemy = null;
        float bestDistance = DetectionRadiusCloth;

        foreach (var healthManager in UObject.FindObjectsOfType<HealthManager>()) {
            GameObject enemyObject = healthManager.gameObject;
            if (!IsValidTarget(enemyObject, selfObject)) continue;

            Vector2 center = ClothSpatialSensor.EnemyCenter(enemyObject);
            float distance = Vector2.Distance(clothCenter, center);
            if (distance >= bestDistance) continue;
            if (Vector2.Distance(heroCenter, center) > ProtectionRadiusHero) continue;
            if (!ClothSpatialSensor.HasLineOfSight(clothCenter, center) && !ClothSpatialSensor.HasLineOfSight(heroCenter, center)) continue;

            closestEnemy = enemyObject;
            bestDistance = distance;
        }

        return closestEnemy;
    }

    /// <summary>
    /// Reúne los enemigos válidos cuyo CUERPO toca la caja. Se ignoran los triggers hijos de un enemigo
    /// (rangos de detección, sensores de visión...) porque son enormes y no forman parte de su cuerpo:
    /// antes provocaban golpes a enemigos lejanos fuera de la hitbox.
    /// </summary>
    private void CollectTargetsInBox(Vector2 boxCenter, Vector2 boxSize, GameObject source, List<HealthManager> results, out bool playerInBox) {
        results.Clear();
        DamagedTargetsBuffer.Clear();
        playerInBox = false;

        // Sin terreno en la consulta: así el buffer no se llena de colliders irrelevantes
        int hitCount = Physics2D.OverlapBoxNonAlloc(boxCenter, boxSize, 0f, OverlapBuffer, ~ClothSpatialSensor.TerrainMask);

        for (int i = 0; i < hitCount; i++) {
            var col = OverlapBuffer[i];
            if (col == null) continue;

            // El Caballero no tiene HealthManager: se detecta aparte (solo en modo prueba/duelo)
            if (AttackPlayerEnabled && !playerInBox && col.GetComponentInParent<HeroController>() != null) {
                playerInBox = true;
                continue;
            }

            var healthManager = col.GetComponentInParent<HealthManager>();
            if (healthManager == null || healthManager.gameObject == source) continue;

            if (col.isTrigger && col.gameObject != healthManager.gameObject) continue;

            if (!DamagedTargetsBuffer.Add(healthManager)) continue;
            if (!IsValidTarget(healthManager.gameObject, source)) continue;

            results.Add(healthManager);
        }

        DamagedTargetsBuffer.Clear();
    }

    /// <summary>
    /// Devuelve el enemigo más cercano que toca la hitbox de ataque (probando la orientación actual y la contraria).
    /// </summary>
    public GameObject? FindEnemyInHitbox(Transform self, GameObject selfObject) {
        float facing = GetFacing(self);

        for (int pass = 0; pass < 2; pass++) {
            float f = pass == 0 ? facing : -facing;
            Vector2 center = GetAttackBoxCenter(self.position, f);
            CollectTargetsInBox(center, AttackBoxSize, selfObject, TargetsBuffer, out _);

            GameObject? best = null;
            float bestDistance = float.MaxValue;
            foreach (var hm in TargetsBuffer) {
                if (hm == null) continue;
                float d = Vector2.Distance(center, ClothSpatialSensor.EnemyCenter(hm.gameObject));
                if (d < bestDistance) {
                    bestDistance = d;
                    best = hm.gameObject;
                }
            }
            TargetsBuffer.Clear();

            if (best != null) return best;
        }

        return null;
    }

    /// <summary>
    /// Daña solo a los enemigos válidos cuyo cuerpo toca la caja de ataque.
    /// </summary>
    public void ExecuteAttackBoxDamage(GameObject source, Vector2 boxCenter, Vector2 boxSize, int damageAmount) {
        CollectTargetsInBox(boxCenter, boxSize, source, TargetsBuffer, out bool playerInBox);

        for (int i = 0; i < TargetsBuffer.Count; i++) {
            var healthManager = TargetsBuffer[i];
            if (healthManager == null) continue;

            Vector2 enemyCenter = ClothSpatialSensor.EnemyCenter(healthManager.gameObject);
            if (Vector2.Distance(boxCenter, enemyCenter) > 2.5f && !ClothSpatialSensor.HasLineOfSight(boxCenter, enemyCenter)) {
                continue;
            }

            HitInstance hitInstance = new() {
                Source = source,
                DamageDealt = damageAmount,
                Direction = source.transform.position.x < healthManager.transform.position.x ? 0f : 180f,
                Multiplier = 1f,
                MagnitudeMultiplier = 1f,
                IgnoreInvulnerable = false
            };

            healthManager.Hit(hitInstance);
        }

        TargetsBuffer.Clear();

        if (playerInBox) {
            OnPlayerHit(source, boxCenter, damageAmount);
        }
    }

    /// <summary>
    /// Punto de extensión del duelo. De momento solo registra el impacto: NO aplica daño al Caballero.
    /// </summary>
    private static void OnPlayerHit(GameObject source, Vector2 boxCenter, int damageAmount) {
        ClothTheWarriorCompanionMod.Instance?.LogDebug($"[Test] La hitbox de Cloth conectó con el Caballero (daño configurado: {damageAmount}). Sin daño aplicado.");
        PlayerHit?.Invoke(source, boxCenter, damageAmount);
    }
}
