using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ClothTheWarriorCompanion.Config;

namespace ClothTheWarriorCompanion.Behaviors;

/// <summary>
/// Evaluación de amenaza defensiva inminente (colisión o ataque entrante hacia Cloth).
/// </summary>
public struct ThreatAssessment {
    public bool IsThreat;
    public float TimeToCollision;
    public float ClosestApproachDistance;
    public Vector2 EnemyVelocity;
    public Vector2 RelativePosition;
    public float ClosingSpeed;
    public bool CanCounterInTime;
    public bool ShouldDodge;
}

/// <summary>
/// Solución cinemática calculada para interceptar a un objetivo móvil con la hitbox de ataque.
/// </summary>
internal struct InterceptSolution {
    public bool Success;
    public Vector2 PredictedTargetPos;
    public Vector2 DesiredClothPos;
    public float DistX;
    public float DistY;
    public float Facing;
    public float StandoffDistance;
    public float EstimatedFlightTime;
    public LaunchSolution Launch;
}

/// <summary>
/// Subsistema de predicción cinemática y análisis de amenazas para Cloth.
/// Resuelve la trayectoria de intercepción balística ofensiva (evitando colisión de cuerpo antes del impacto de hitbox)
/// y analiza amenazas entrantes mediante tiempo a colisión (TTC) y punto de máxima aproximación (CPA).
/// </summary>
internal sealed class ClothTrajectoryPredictor {
    private static ClothSettings Settings => ClothSettings.Current;

    private readonly Dictionary<GameObject, Queue<(float time, Vector2 pos)>> _targetHistories = new();

    public void Track(GameObject? obj, Vector2 pos) {
        if (obj == null) return;
        if (!_targetHistories.TryGetValue(obj, out var q)) {
            q = new Queue<(float time, Vector2 pos)>();
            _targetHistories[obj] = q;
        }
        q.Enqueue((Time.time, pos));
        while (q.Count > 15) {
            q.Dequeue();
        }
    }

    public void CleanUp(Func<GameObject, bool> isTargetValid) {
        var keys = _targetHistories.Keys.ToList();
        for (int i = 0; i < keys.Count; i++) {
            var k = keys[i];
            if (k == null || !k.activeInHierarchy || !isTargetValid(k)) {
                if (k != null) {
                    _targetHistories.Remove(k);
                }
            }
        }
    }

    /// <summary>
    /// Estima la velocidad actual del enemigo. Prioriza la velocidad del Rigidbody2D instantáneo;
    /// si no dispone de física pura, recurre a regresión por historial de posiciones.
    /// </summary>
    public Vector2 GetVelocity(GameObject enemy) {
        if (enemy == null) return Vector2.zero;

        var rb = enemy.GetComponent<Rigidbody2D>();
        if (rb != null && rb.velocity.sqrMagnitude > 0.01f) {
            return new Vector2(
                Mathf.Clamp(rb.velocity.x, -35f, 35f),
                Mathf.Clamp(rb.velocity.y, -35f, 35f)
            );
        }

        if (_targetHistories.TryGetValue(enemy, out var q) && q.Count >= 2) {
            var oldest = q.Peek();
            var newest = q.Last();
            float dt = newest.time - oldest.time;
            if (dt > 0.02f) {
                Vector2 v = (newest.pos - oldest.pos) / dt;
                return new Vector2(
                    Mathf.Clamp(v.x, -35f, 35f),
                    Mathf.Clamp(v.y, -35f, 35f)
                );
            }
        }

        return Vector2.zero;
    }

    /// <summary>
    /// Predice la posición futura del objetivo tras un tiempo determinado.
    /// </summary>
    public Vector2 PredictPosition(GameObject enemy, float timeAhead) {
        Vector2 currentPos = ClothSpatialSensor.EnemyCenter(enemy);
        if (!Settings.PredictTargetMovement || enemy == null) {
            return currentPos;
        }

        Vector2 velocity = GetVelocity(enemy);
        if (velocity.sqrMagnitude < 0.01f) {
            return currentPos;
        }

        float time = Mathf.Clamp(timeAhead, 0f, Settings.PredictionMaxTime);
        Vector2 futurePos = currentPos + (velocity * time);

        float maxDist = Settings.PredictionMaxDistance;
        if (Vector2.Distance(currentPos, futurePos) > maxDist) {
            futurePos = currentPos + (futurePos - currentPos).normalized * maxDist;
        }

        return futurePos;
    }

    /// <summary>
    /// Resuelve iterativamente la trayectoria de intercepción balística para impactar al enemigo en movimiento.
    /// Considera las dimensiones de la hitbox de Cloth y los límites del collider del enemigo para calcular un
    /// espaciado óptimo que impida que el cuerpo físico de Cloth impacte antes que la caja de daño.
    /// </summary>
    public bool TryCalculateAttackIntercept(
        Vector2 clothPos,
        float clothBodyRadius,
        GameObject enemy,
        float maxSpeedX,
        float diveMultiplier,
        float ceilingClearance,
        float jumpHeightOverride,
        float apexTimeOverride,
        out InterceptSolution solution) {

        solution = default;
        if (enemy == null) return false;

        Vector2 currentEnemyCenter = ClothSpatialSensor.EnemyCenter(enemy);
        float facing = currentEnemyCenter.x >= clothPos.x ? 1f : -1f;

        // Dimensiones del enemigo
        var enemyCol = ClothSpatialSensor.GetBodyCollider(enemy);
        float enemyExtentsX = enemyCol != null ? enemyCol.bounds.extents.x : 1.0f;
        float enemyExtentsY = enemyCol != null ? enemyCol.bounds.extents.y : 1.0f;

        // Dimensiones de la hitbox de ataque
        float boxOffsetX = Settings.AttackBoxOffsetX;
        float boxOffsetY = Settings.AttackBoxOffsetY;
        float boxWidth = Mathf.Max(0.5f, Settings.AttackBoxWidth);
        float boxHeight = Mathf.Max(0.5f, Settings.AttackBoxHeight);
        float margin = Mathf.Max(0.1f, Settings.HitboxClearanceMargin);

        // Distancia óptima entre el transform de Cloth y el centro del enemigo:
        // Debe ser al menos el radio del cuerpo de Cloth + el tamaño del enemigo + margen de seguridad.
        float minSafeStandoff = clothBodyRadius + enemyExtentsX + margin;
        float idealStandoff = Mathf.Max(boxOffsetX, minSafeStandoff);

        // Limitar para que el enemigo no quede fuera del alcance frontal de la hitbox
        float maxReachStandoff = boxOffsetX + (boxWidth * 0.5f) - 0.25f;
        if (idealStandoff > maxReachStandoff) {
            idealStandoff = Mathf.Max(minSafeStandoff, maxReachStandoff);
        }

        // Estimación inicial del tiempo de vuelo
        float t = apexTimeOverride > 0f ? apexTimeOverride * 2.0f : 0.65f;
        LaunchSolution currentLaunch = default;
        Vector2 predictedPos = currentEnemyCenter;

        // Solucionador de punto fijo (Fixed-point iteration) para converger en el tiempo y posición exacta
        int maxIterations = 4;
        for (int i = 0; i < maxIterations; i++) {
            predictedPos = PredictPosition(enemy, t);

            // Ajuste horizontal y vertical respecto al punto de impacto deseado
            float targetClothX = predictedPos.x - (facing * idealStandoff);
            float targetClothY = predictedPos.y - boxOffsetY;

            float dx = targetClothX - clothPos.x;
            float dy = targetClothY - clothPos.y;

            currentLaunch = ClothMotor.ComputeLaunch(
                dx, dy,
                maxSpeedX,
                diveMultiplier,
                ceilingClearance,
                jumpHeightOverride,
                apexTimeOverride,
                facing
            );

            float tNext = currentLaunch.TotalFlightTime;
            if (Mathf.Abs(tNext - t) < 0.015f) {
                t = tNext;
                break;
            }

            // Relajación para convergencia estable
            t = 0.5f * (t + tNext);
        }

        solution = new InterceptSolution {
            Success = true,
            PredictedTargetPos = predictedPos,
            DesiredClothPos = new Vector2(predictedPos.x - (facing * idealStandoff), predictedPos.y - boxOffsetY),
            DistX = (predictedPos.x - (facing * idealStandoff)) - clothPos.x,
            DistY = (predictedPos.y - boxOffsetY) - clothPos.y,
            Facing = facing,
            StandoffDistance = idealStandoff,
            EstimatedFlightTime = t,
            Launch = currentLaunch
        };

        return true;
    }

    /// <summary>
    /// Evalúa si el enemigo se aproxima en trayectoria de colisión directa contra Cloth mediante TTC y CPA.
    /// </summary>
    public ThreatAssessment CheckCollisionThreat(
        Vector2 clothCenter,
        float clothBodyRadius,
        GameObject enemy,
        float attackCooldown,
        float attackWindupTime) {

        ThreatAssessment threat = default;
        if (!Settings.DefensivePredictionEnabled || enemy == null) {
            return threat;
        }

        Vector2 enemyCenter = ClothSpatialSensor.EnemyCenter(enemy);
        Vector2 enemyVelocity = GetVelocity(enemy);

        threat.EnemyVelocity = enemyVelocity;
        threat.RelativePosition = enemyCenter - clothCenter;

        if (enemyVelocity.sqrMagnitude < 0.15f) {
            return threat;
        }

        Vector2 r = enemyCenter - clothCenter;
        float rDotV = Vector2.Dot(r, enemyVelocity);

        // Si r · V >= 0, la distancia aumenta (se aleja o se mueve tangencialmente en sentido opuesto)
        if (rDotV >= 0f) {
            return threat;
        }

        float sv = enemyVelocity.sqrMagnitude;
        float tCpa = -rDotV / sv;

        // Fuera de la ventana de anticipación
        if (tCpa <= 0f || tCpa > Settings.ThreatLookaheadTime) {
            return threat;
        }

        Vector2 cpaPos = r + (enemyVelocity * tCpa);
        float dCpa = cpaPos.magnitude;
        threat.ClosestApproachDistance = dCpa;

        var enemyCol = ClothSpatialSensor.GetBodyCollider(enemy);
        float enemyRadius = enemyCol != null ? Mathf.Max(enemyCol.bounds.extents.x, enemyCol.bounds.extents.y) : 1.1f;
        float dangerRadius = clothBodyRadius + enemyRadius + Settings.ThreatMargin;

        if (dCpa > dangerRadius) {
            return threat; // Pasa de largo sin tocar a Cloth
        }

        // Colisión geométrica inminente confirmada: calcular tiempo exacto de contacto (TTC)
        float discriminant = (rDotV * rDotV) - (sv * (r.sqrMagnitude - (dangerRadius * dangerRadius)));
        float ttc = discriminant >= 0f
            ? (-rDotV - Mathf.Sqrt(discriminant)) / sv
            : tCpa;

        ttc = Mathf.Max(0.02f, ttc);

        threat.IsThreat = true;
        threat.TimeToCollision = ttc;
        threat.ClosingSpeed = -rDotV / Mathf.Max(0.1f, r.magnitude);

        // ¿Tiene Cloth tiempo suficiente para preparar y conectar un ataque antes del impacto?
        threat.CanCounterInTime = (attackCooldown <= 0f) && (ttc >= (attackWindupTime + 0.12f));
        threat.ShouldDodge = !threat.CanCounterInTime;

        return threat;
    }

    /// <summary>
    /// Comprueba si un punto de aterrizaje propuesto se encuentra dentro del corredor de peligro
    /// o línea de fuego de una amenaza que se desplaza a gran velocidad.
    /// </summary>
    public bool IsPointInTrajectoryCorridor(
        Vector2 point,
        Vector2 threatCenter,
        Vector2 threatVelocity,
        Vector2 threatExtents,
        float lookaheadTime,
        float safetyMargin) {

        float speedSqr = threatVelocity.sqrMagnitude;
        float safeX = threatExtents.x + safetyMargin;
        float safeY = threatExtents.y + safetyMargin;

        // Si la amenaza está prácticamente estática
        if (speedSqr < 0.2f) {
            return Mathf.Abs(point.x - threatCenter.x) <= safeX
                && Mathf.Abs(point.y - threatCenter.y) <= safeY;
        }

        // Proyección del segmento de trayectoria
        Vector2 endPos = threatCenter + (threatVelocity * Mathf.Clamp(lookaheadTime, 0.2f, 2.0f));
        Vector2 segment = endPos - threatCenter;
        float segLenSqr = segment.sqrMagnitude;

        Vector2 toPoint = point - threatCenter;
        float t = Mathf.Clamp01(Vector2.Dot(toPoint, segment) / segLenSqr);
        Vector2 closestOnTrajectory = threatCenter + (segment * t);

        float distX = Mathf.Abs(point.x - closestOnTrajectory.x);
        float distY = Mathf.Abs(point.y - closestOnTrajectory.y);

        return distX <= safeX && distY <= safeY;
    }
}
