namespace ClothTheWarriorCompanion.Behaviors;

/// <summary>
/// Gestiona la detección, validación y combate con enemigos, así como el daño en área por impacto (slam) de Cloth.
/// </summary>
internal sealed class ClothCombatHandler {
    public const int DefaultSlamDamageAmount = 40;
    public const float DefaultSlamRadius = 5f;

    public const float AttackRange = 18f;
    public const float AttackMaxSpeedX = 24f;
    public const float DiveGravityMultiplier = 2.5f;
    public const float AttackAnimDistance = 6f;
    public const float AirHitDistance = 2.5f;

    private static readonly Collider2D[] OverlapCircleBuffer = new Collider2D[32];
    private static readonly HashSet<HealthManager> DamagedTargetsBuffer = new();

    public bool IsValidTarget(GameObject? target, GameObject selfObject) {
        if (target == null || target == selfObject) return false;
        var healthManager = target.GetComponent<HealthManager>();
        return healthManager != null && healthManager.isActiveAndEnabled && !healthManager.isDead && !healthManager.IsInvincible && healthManager.hp > 0;
    }

    public GameObject? FindBestEnemy(Vector2 clothCenter, Vector2 heroCenter, GameObject selfObject) {
        GameObject? closestEnemy = null;
        float bestDistance = 22f;

        foreach (var healthManager in UObject.FindObjectsOfType<HealthManager>()) {
            GameObject enemyObject = healthManager.gameObject;
            if (!IsValidTarget(enemyObject, selfObject)) continue;

            Vector2 center = ClothSpatialSensor.EnemyCenter(enemyObject);
            float distance = Vector2.Distance(clothCenter, center);
            if (distance >= bestDistance) continue;
            if (Vector2.Distance(heroCenter, center) > 25f) continue;
            if (!ClothSpatialSensor.HasLineOfSight(clothCenter, center) && !ClothSpatialSensor.HasLineOfSight(heroCenter, center)) continue;

            closestEnemy = enemyObject;
            bestDistance = distance;
        }

        return closestEnemy;
    }

    public void ExecuteSlamAreaDamage(GameObject source, Vector2 center, float radius, int damageAmount) {
        DamagedTargetsBuffer.Clear();

        int hitCount = Physics2D.OverlapCircleNonAlloc(center, radius, OverlapCircleBuffer);

        for (int i = 0; i < hitCount; i++) {
            var col = OverlapCircleBuffer[i];
            if (col == null) continue;

            var healthManager = col.GetComponentInParent<HealthManager>();
            if (healthManager == null || healthManager.gameObject == source || !DamagedTargetsBuffer.Add(healthManager)) {
                continue;
            }

            if (!IsValidTarget(healthManager.gameObject, source)) continue;

            Vector2 enemyCenter = ClothSpatialSensor.EnemyCenter(healthManager.gameObject);
            if (Vector2.Distance(center, enemyCenter) > 2.5f && !ClothSpatialSensor.HasLineOfSight(center, enemyCenter)) {
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

        DamagedTargetsBuffer.Clear();
    }
}

