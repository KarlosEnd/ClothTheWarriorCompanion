using ClothTheWarriorCompanion.Config;

namespace ClothTheWarriorCompanion.Behaviors;

/// <summary>
/// Gestiona la detección, validación y combate con enemigos, así como el daño en área por impacto (slam) de Cloth.
/// </summary>
internal sealed class ClothCombatHandler {
    #region Tuning & Configuration

    private static ClothSettings Settings => ClothSettings.Current;

    public static int DefaultSlamDamageAmount => Settings.SlamDamageAmount;
    public static float DefaultSlamRadius => Settings.SlamRadius;
    public static float AttackRange => Settings.AttackRange;
    public static float AttackMaxSpeedX => Settings.AttackMaxSpeedX;
    public static float DiveGravityMultiplier => Settings.DiveGravityMultiplier;
    public static float AttackAnimDistance => Settings.AttackAnimDistance;
    public static float AirHitDistance => Settings.AirHitDistance;
    public static float DetectionRadiusCloth => Settings.DetectionRadiusCloth;
    public static float ProtectionRadiusHero => Settings.ProtectionRadiusHero;

    #endregion

    private static readonly Collider2D[] OverlapCircleBuffer = new Collider2D[32];
    private static readonly HashSet<HealthManager> DamagedTargetsBuffer = new();

    public bool IsValidTarget(GameObject? target, GameObject selfObject) {
        if (target == null || target == selfObject) return false;
        var healthManager = target.GetComponent<HealthManager>();
        return healthManager != null && healthManager.isActiveAndEnabled && !healthManager.isDead && !healthManager.IsInvincible && healthManager.hp > 0;
    }

    public GameObject? FindBestEnemy(Vector2 clothCenter, Vector2 heroCenter, GameObject selfObject) {
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
