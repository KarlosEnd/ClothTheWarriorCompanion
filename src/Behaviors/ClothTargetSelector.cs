using System;
using System.Collections.Generic;
using UnityEngine;
using ClothTheWarriorCompanion.Config;

namespace ClothTheWarriorCompanion.Behaviors;

/// <summary>
/// Contiene la evaluación completa de un objetivo evaluado por el sistema de prioridades.
/// </summary>
public struct RankedTarget {
    public GameObject Enemy;
    public float PriorityScore;
    public float DistanceToCloth;
    public float DistanceToHero;
    public float Speed;
    public float ApproachSpeedCloth;
    public float ApproachSpeedHero;
    public ThreatAssessment Threat;
    public bool HasLineOfSight;
}

/// <summary>
/// Gestor de selección y priorización de objetivos múltiples en dos fases:
/// Fase 1: Algoritmo de cribado y ponderación según distancia, velocidad, vector de aproximación y protección.
/// Fase 2: Análisis táctico detallado (anticipación de amenazas TTC/CPA y línea de visión) en los candidatos líderes.
/// </summary>
internal sealed class ClothTargetSelector {
    private static ClothSettings Settings => ClothSettings.Current;

    private readonly List<RankedTarget> _rankedTargets = new();
    private readonly List<HealthManager> _cachedHealthManagers = new();
    private float _cacheTimer = 0f;

    public IReadOnlyList<RankedTarget> AllRankedTargets => _rankedTargets;
    public RankedTarget? BestTarget => _rankedTargets.Count > 0 ? _rankedTargets[0] : null;

    /// <summary>
    /// Devuelve si existe algún objetivo con amenaza inminente de colisión que requiera esquiva o intercepción inmediata.
    /// </summary>
    public bool HasImminentThreat(out RankedTarget threatTarget) {
        for (int i = 0; i < _rankedTargets.Count; i++) {
            var candidate = _rankedTargets[i];
            if (candidate.Threat.IsThreat) {
                threatTarget = candidate;
                return true;
            }
        }
        threatTarget = default;
        return false;
    }

    /// <summary>
    /// Escanea todos los objetivos activos y los ordena por prioridad táctica.
    /// </summary>
    public void UpdateTargets(
        Vector2 clothCenter,
        float clothBodyRadius,
        Vector2 heroCenter,
        GameObject selfObject,
        ClothTrajectoryPredictor predictor,
        ClothCombatHandler combat,
        float attackCooldown,
        float attackWindupTime) {

        _rankedTargets.Clear();

        // 1. Refrescar lista de HealthManagers cada cierto intervalo para no saturar FindObjectsOfType
        _cacheTimer -= Time.deltaTime;
        if (_cacheTimer <= 0f) {
            _cacheTimer = 0.2f;
            _cachedHealthManagers.Clear();
            var allHms = UObject.FindObjectsOfType<HealthManager>();
            for (int i = 0; i < allHms.Length; i++) {
                var hm = allHms[i];
                if (hm != null && hm.gameObject != selfObject && hm.isActiveAndEnabled && !hm.isDead && !hm.IsInvincible && hm.hp > 0) {
                    _cachedHealthManagers.Add(hm);
                }
            }
        }

        // 2. Considerar al Caballero en modo duelo si está habilitado
        if (ClothCombatHandler.AttackPlayerEnabled) {
            var hero = HeroController.instance;
            if (hero != null && combat.IsValidTarget(hero.gameObject, selfObject)) {
                EvaluateCandidate(
                    hero.gameObject,
                    clothCenter,
                    clothBodyRadius,
                    heroCenter,
                    predictor,
                    attackCooldown,
                    attackWindupTime,
                    isPlayer: true
                );
            }
        }

        // 3. FASE 1: Filtrado y puntuación heurística de todos los enemigos en rango
        for (int i = 0; i < _cachedHealthManagers.Count; i++) {
            var hm = _cachedHealthManagers[i];
            if (hm == null) continue;
            GameObject enemy = hm.gameObject;
            if (!combat.IsValidTarget(enemy, selfObject)) continue;

            EvaluateCandidate(
                enemy,
                clothCenter,
                clothBodyRadius,
                heroCenter,
                predictor,
                attackCooldown,
                attackWindupTime,
                isPlayer: false
            );
        }

        // 3.5. Detección de ataques y proyectiles de enemigos (DamageHero / damages_hero sin HealthManager)
        ScanIncomingProjectiles(
            clothCenter,
            clothBodyRadius,
            heroCenter,
            selfObject,
            predictor,
            attackCooldown,
            attackWindupTime
        );

        // 4. FASE 2: Ordenar candidatos de mayor a menor prioridad
        _rankedTargets.Sort((a, b) => b.PriorityScore.CompareTo(a.PriorityScore));

        // Validación táctica de los mejores candidatos (preferir aquellos con línea de visión)
        if (_rankedTargets.Count > 1) {
            // Si el #1 no tiene línea de visión pero el #2 sí y está muy cerca en puntuación, priorizar el visible
            if (!_rankedTargets[0].HasLineOfSight && _rankedTargets[1].HasLineOfSight &&
                (_rankedTargets[0].PriorityScore - _rankedTargets[1].PriorityScore) < 0.25f) {
                var temp = _rankedTargets[0];
                _rankedTargets[0] = _rankedTargets[1];
                _rankedTargets[1] = temp;
            }
        }
    }

    private readonly Collider2D[] _threatScanBuffer = new Collider2D[48];

    private void ScanIncomingProjectiles(
        Vector2 clothCenter,
        float clothBodyRadius,
        Vector2 heroCenter,
        GameObject selfObject,
        ClothTrajectoryPredictor predictor,
        float attackCooldown,
        float attackWindupTime) {

        int hitCount = Physics2D.OverlapCircleNonAlloc(clothCenter, Settings.DetectionRadiusCloth, _threatScanBuffer, ~ClothSpatialSensor.TerrainMask);
        for (int i = 0; i < hitCount; i++) {
            var col = _threatScanBuffer[i];
            if (col == null || col.transform.IsChildOf(selfObject.transform)) continue;
            if (HeroController.instance != null && col.transform.IsChildOf(HeroController.instance.transform)) continue;

            var damageHero = col.GetComponent<DamageHero>();
            bool hasDamageHeroFsm = damageHero == null && FSMUtility.ContainsFSM(col.gameObject, "damages_hero");
            if (damageHero == null && !hasDamageHeroFsm) continue;
            if (damageHero != null && (damageHero.hazardType > 1 || damageHero.damageDealt <= 0)) continue;

            // Ignorar si pertenece a un enemigo que ya tiene HealthManager (ya fue evaluado)
            var parentHm = col.GetComponentInParent<HealthManager>();
            if (parentHm != null) continue;

            GameObject projectile = col.gameObject;
            Vector2 projCenter = col.bounds.center;
            predictor.Track(projectile, projCenter);

            ThreatAssessment threat = predictor.CheckCollisionThreat(
                clothCenter,
                clothBodyRadius,
                projectile,
                attackCooldown,
                attackWindupTime
            );

            if (threat.IsThreat) {
                _rankedTargets.Add(new RankedTarget {
                    Enemy = projectile,
                    PriorityScore = 5.0f + (1f / Mathf.Max(0.05f, threat.TimeToCollision)),
                    DistanceToCloth = Vector2.Distance(clothCenter, projCenter),
                    DistanceToHero = Vector2.Distance(heroCenter, projCenter),
                    Speed = threat.EnemyVelocity.magnitude,
                    ApproachSpeedCloth = threat.ClosingSpeed,
                    ApproachSpeedHero = 0f,
                    Threat = threat,
                    HasLineOfSight = true
                });
            }
        }
    }

    private void EvaluateCandidate(
        GameObject enemy,
        Vector2 clothCenter,
        float clothBodyRadius,
        Vector2 heroCenter,
        ClothTrajectoryPredictor predictor,
        float attackCooldown,
        float attackWindupTime,
        bool isPlayer) {

        Vector2 enemyCenter = ClothSpatialSensor.EnemyCenter(enemy);

        // Alimentar historial del predictor
        predictor.Track(enemy, enemyCenter);

        float distCloth = Vector2.Distance(clothCenter, enemyCenter);
        float distHero = Vector2.Distance(heroCenter, enemyCenter);

        // Fuera del radio de detección o protección
        if (distCloth > Settings.DetectionRadiusCloth) return;
        if (!isPlayer && distHero > Settings.ProtectionRadiusHero) return;

        bool hasLosCloth = ClothSpatialSensor.HasLineOfSight(clothCenter, enemyCenter);
        bool hasLosHero = ClothSpatialSensor.HasLineOfSight(heroCenter, enemyCenter);
        if (!hasLosCloth && !hasLosHero) return;

        Vector2 vel = predictor.GetVelocity(enemy);
        float speed = vel.magnitude;

        // Vector de aproximación hacia Cloth:
        // Positivo = viene directamente hacia Cloth; negativo = se aleja
        Vector2 dirCloth = (enemyCenter - clothCenter);
        float distMagCloth = Mathf.Max(0.1f, dirCloth.magnitude);
        float approachCloth = -Vector2.Dot(dirCloth / distMagCloth, vel);

        // Vector de aproximación hacia el Caballero (para defenderlo de cargas enemigas)
        Vector2 dirHero = (enemyCenter - heroCenter);
        float distMagHero = Mathf.Max(0.1f, dirHero.magnitude);
        float approachHero = -Vector2.Dot(dirHero / distMagHero, vel);

        // Análisis defensivo mediante TTC y CPA
        ThreatAssessment threat = predictor.CheckCollisionThreat(
            clothCenter,
            clothBodyRadius,
            enemy,
            attackCooldown,
            attackWindupTime
        );

        // Puntuaciones normalizadas (0 a 1)
        float distScore = 1f - Mathf.Clamp01(distCloth / Mathf.Max(1f, Settings.DetectionRadiusCloth));
        float speedScore = Mathf.Clamp01(speed / 20f);
        float appClothScore = approachCloth > 0f ? Mathf.Clamp01(approachCloth / 20f) : 0f;
        float appHeroScore = approachHero > 0f ? Mathf.Clamp01(approachHero / 20f) : 0f;

        // Bonificaciones de contexto
        float bossBonus = enemy.CompareTag("Boss") ? 0.35f : 0f;
        float threatBonus = threat.IsThreat ? 0.6f : 0f;
        float losBonus = hasLosCloth ? 0.2f : 0f;

        // Puntuación combinada según pesos configurables
        float totalScore =
            (distScore * Settings.TargetPriorityDistanceWeight) +
            (speedScore * Settings.TargetPrioritySpeedWeight) +
            (appClothScore * Settings.TargetPriorityApproachWeight) +
            (appHeroScore * Settings.TargetPriorityKnightProtectionWeight) +
            bossBonus + threatBonus + losBonus;

        _rankedTargets.Add(new RankedTarget {
            Enemy = enemy,
            PriorityScore = totalScore,
            DistanceToCloth = distCloth,
            DistanceToHero = distHero,
            Speed = speed,
            ApproachSpeedCloth = approachCloth,
            ApproachSpeedHero = approachHero,
            Threat = threat,
            HasLineOfSight = hasLosCloth
        });
    }
}

