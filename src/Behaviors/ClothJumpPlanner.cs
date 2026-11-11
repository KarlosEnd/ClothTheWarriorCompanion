using ClothTheWarriorCompanion.Config;

namespace ClothTheWarriorCompanion.Behaviors;

/// <summary>
/// Plan de movimiento entre dos puntos de suelo: salto directo, o salto corto de posicionamiento seguido del salto largo.
/// Height/Apex son los overrides del salto largo (-1 = calculados automáticamente).
/// </summary>
internal struct TravelPlan {
    public bool NeedsHop;
    public Vector2 HopFeet;
    public float Height;
    public float Apex;
}

/// <summary>
/// Plan de ataque: salto de ataque directo, o salto corto de posicionamiento seguido del ataque.
/// </summary>
internal struct AttackPlan {
    public bool NeedsHop;
    public Vector2 HopFeet;
    public float Height;
    public float Apex;
}

internal struct SimResult {
    /// <summary>Choca con pared, techo o un borde antes de llegar.</summary>
    public bool Blocked;
    /// <summary>Aterriza sobre una superficie de suelo.</summary>
    public bool Landed;
    /// <summary>La hitbox de ataque llegó a tocar al enemigo durante la trayectoria.</summary>
    public bool ReachedEnemy;
    public Vector2 LandingPoint;
}

/// <summary>
/// Simula los saltos de Cloth antes de ejecutarlos: reproduce la parábola de ClothMotor.Launch y la barre contra el
/// terreno con un círculo del tamaño de su collider de salto. Con eso decide si un salto llega, si necesita una
/// variante más alta o un salto corto de posicionamiento previo, y dónde puede aterrizar/escapar con seguridad.
/// </summary>
internal sealed class ClothJumpPlanner {
    private static ClothSettings Settings => ClothSettings.Current;

    private const float HopTolerance = 1.5f;

    private readonly Transform _transform;
    private readonly ClothMotor _motor;
    private readonly ClothSpatialSensor _sensor;

    private readonly List<float> _variants = new();
    private readonly List<Vector2> _candidates = new();

    public ClothJumpPlanner(Transform transform, ClothMotor motor, ClothSpatialSensor sensor) {
        _transform = transform;
        _motor = motor;
        _sensor = sensor;
    }

    #region Simulation

    private static Vector2 PositionAt(Vector2 start, in LaunchSolution sol, float t) {
        float g = sol.EffectiveGravity;
        float x = start.x + (sol.VelocityX * t);
        float apex = sol.ApexTime;
        float y;

        if (t <= apex) {
            y = start.y + (sol.VelocityY * t) - (0.5f * g * t * t);
        } else {
            float yApex = start.y + (sol.VelocityY * apex) - (0.5f * g * apex * apex);
            float vApex = sol.VelocityY - (g * apex);
            float dt = t - apex;
            y = yApex + (vApex * dt) - (0.5f * g * sol.DiveMultiplier * dt * dt);
        }

        return new Vector2(x, y);
    }

    private static bool ReachesEnemy(Vector2 bodyPosition, float facing, UnityEngine.Bounds enemyBounds) {
        Vector2 c = ClothCombatHandler.GetAttackBoxCenter(bodyPosition, facing);
        Vector2 size = ClothCombatHandler.AttackBoxSize;
        return Mathf.Abs(c.x - enemyBounds.center.x) <= (size.x * 0.5f) + enemyBounds.extents.x
            && Mathf.Abs(c.y - enemyBounds.center.y) <= (size.y * 0.5f) + enemyBounds.extents.y;
    }

    /// <summary>
    /// Recorre la trayectoria y la barre contra el terreno. Si se pasa un enemigo, también detecta si la hitbox lo alcanza.
    /// </summary>
    private SimResult Simulate(Vector2 startPos, in LaunchSolution sol, GameObject? enemy, float facing) {
        var result = new SimResult();

        float radius = Mathf.Max(0.1f, _motor.JumpCircleRadius * Mathf.Clamp(Settings.JumpPlanRadiusScale, 0.3f, 1.2f));
        Vector2 lift = new(0f, _motor.JumpCircleOffsetY + 0.1f);
        float step = Mathf.Clamp(Settings.JumpPlanStep, 0.02f, 0.2f);
        float tMax = sol.TotalFlightTime + 1f;

        UnityEngine.Bounds enemyBounds = default;
        bool hasEnemy = false;
        if (enemy != null) {
            var col = ClothSpatialSensor.GetBodyCollider(enemy);
            if (col != null) {
                enemyBounds = col.bounds;
                hasEnemy = true;
            }
        }

        Vector2 prev = startPos + lift;
        for (float t = step; t <= tMax; t += step) {
            Vector2 pos = PositionAt(startPos, sol, t);
            Vector2 center = pos + lift;
            Vector2 delta = center - prev;
            float dist = delta.magnitude;

            if (dist > 0.0001f) {
                RaycastHit2D hit = Physics2D.CircleCast(prev, radius, delta / dist, dist, ClothSpatialSensor.TerrainMask);

                // Un solape inicial (distancia 0) en el primer paso no cuenta como obstáculo
                if (hit.collider != null && !(hit.distance <= 0.0001f && t <= step * 1.5f)) {
                    if (hasEnemy && ReachesEnemy(hit.centroid - lift, facing, enemyBounds))
                        result.ReachedEnemy = true;

                    if (hit.normal.y > 0.55f) {
                        result.Landed = true;
                        result.LandingPoint = hit.point;
                    } else {
                        result.Blocked = true;
                    }
                    return result;
                }
            }

            prev = center;

            if (hasEnemy && ReachesEnemy(pos, facing, enemyBounds)) {
                result.ReachedEnemy = true;
                return result;
            }
        }

        return result;
    }

    private bool LandsAt(in SimResult sim, Vector2 target, float tolX, float tolY) =>
        sim.Landed
        && Mathf.Abs(sim.LandingPoint.x - target.x) <= tolX
        && Mathf.Abs(sim.LandingPoint.y - target.y) <= tolY;

    /// <summary>
    /// Alturas a probar: la calculada (-1) y varias más altas hasta el máximo permitido.
    /// </summary>
    private void BuildHeightVariants(float baseHeight) {
        _variants.Clear();
        _variants.Add(-1f);

        int count = Mathf.Max(0, Settings.JumpHeightVariants);
        float step = Mathf.Max(0.1f, Settings.JumpHeightVariantStep);
        float maxHeight = Settings.MaxJumpHeight;

        for (int i = 1; i <= count; i++) {
            float h = Mathf.Min(baseHeight + (step * i), maxHeight);
            if (h <= baseHeight + 0.05f)
                break;
            _variants.Add(h);
        }
    }

    #endregion

    #region Travel

    /// <summary>
    /// Busca un salto directo (con variantes de altura) que llegue de startFeet a targetFeet sin chocar.
    /// </summary>
    public bool TryFindTravelVariant(Vector2 startFeet, Vector2 targetFeet, out float height, out float apex) {
        height = -1f;
        apex = -1f;

        Vector2 startPos = _sensor.FeetToPosition(startFeet);
        float dx = targetFeet.x - startFeet.x;
        float dy = targetFeet.y - startFeet.y;
        float ceiling = _sensor.GetCeilingClearanceAt(startPos);
        float dive = Mathf.Max(1f, Settings.JumpFallGravityMultiplier);
        float speed = Settings.JumpNormalMaxSpeedX;

        var first = ClothMotor.ComputeLaunch(dx, dy, speed, dive, ceiling);
        BuildHeightVariants(first.JumpHeight);

        for (int i = 0; i < _variants.Count; i++) {
            float ov = _variants[i];
            var sol = ov < 0f ? first : ClothMotor.ComputeLaunch(dx, dy, speed, dive, ceiling, ov, -1f);
            var sim = Simulate(startPos, sol, null, 0f);
            if (LandsAt(sim, targetFeet, Settings.LandingToleranceX, Settings.LandingToleranceY)) {
                height = ov;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Plan de viaje: salto directo si es viable; si no, salto corto de posicionamiento y luego el salto largo.
    /// </summary>
    public bool TryPlanTravel(Vector2 myFeet, Vector2 targetFeet, out TravelPlan plan) {
        plan = default;
        plan.Height = -1f;
        plan.Apex = -1f;

        if (TryFindTravelVariant(myFeet, targetFeet, out float h, out float a)) {
            plan.Height = h;
            plan.Apex = a;
            return true;
        }

        if (!Settings.HopPlanningEnabled)
            return false;

        BuildHopCandidates(myFeet);
        for (int i = 0; i < _candidates.Count; i++) {
            Vector2 candidate = _candidates[i];
            if (!ValidateHop(myFeet, candidate))
                continue;

            if (TryFindTravelVariant(candidate, targetFeet, out h, out a)) {
                plan.NeedsHop = true;
                plan.HopFeet = candidate;
                plan.Height = h;
                plan.Apex = a;
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Hop (positioning)

    /// <summary>
    /// Puntos de suelo cercanos (de menor a mayor distancia, a ambos lados) donde Cloth podría posicionarse.
    /// </summary>
    private void BuildHopCandidates(Vector2 myFeet) {
        _candidates.Clear();

        float step = Mathf.Max(0.5f, Settings.HopSearchStep);
        float max = Mathf.Max(step, Settings.HopSearchMaxDistance);

        for (float offset = step; offset <= max + 0.01f; offset += step) {
            for (int s = -1; s <= 1; s += 2) {
                Vector2 probe = new(myFeet.x + (s * offset), myFeet.y + 1.5f);
                if (!ClothCompanionManager.TryFindGround(probe, out Vector2 ground, 4f))
                    continue;
                if (Mathf.Abs(ground.y - myFeet.y) > Settings.HopMaxDeltaY)
                    continue;
                if (_sensor.IsBlockedAt(_sensor.FeetToPosition(ground)))
                    continue;
                _candidates.Add(ground);
            }
        }
    }

    /// <summary>
    /// Comprueba que el salto corto de posicionamiento (parámetros de "Ataque Corto") aterriza en el punto indicado.
    /// </summary>
    private bool ValidateHop(Vector2 startFeet, Vector2 landFeet) {
        Vector2 startPos = _sensor.FeetToPosition(startFeet);
        float dx = landFeet.x - startFeet.x;
        float dy = landFeet.y - startFeet.y;

        var sol = ClothMotor.ComputeLaunch(
            dx, dy,
            Settings.CloseAttackSpeedX,
            Mathf.Max(1f, Settings.CloseDiveGravityMultiplier),
            _sensor.GetCeilingClearanceAt(startPos),
            Settings.CloseJumpHeight,
            Settings.CloseApexTime,
            Mathf.Abs(dx) > 0.01f ? Mathf.Sign(dx) : 0f);

        var sim = Simulate(startPos, sol, null, 0f);
        return LandsAt(sim, landFeet, HopTolerance, HopTolerance);
    }

    #endregion

    #region Attack

    private bool TryAttackVariant(Vector2 startPos, GameObject enemy, out float height, out float apex) {
        height = -1f;
        apex = -1f;

        Vector2 enemyCenter = ClothSpatialSensor.EnemyCenter(enemy);
        float facing = enemyCenter.x >= startPos.x ? 1f : -1f;
        Vector2 attackCenter = ClothCombatHandler.GetAttackBoxCenter(startPos, facing);

        bool isClose = ClothCombatHandler.CloseAttackDistance > 0f
            && Vector2.Distance(attackCenter, enemyCenter) <= ClothCombatHandler.CloseAttackDistance
            && Mathf.Abs(enemyCenter.y - attackCenter.y) <= ClothCombatHandler.CloseAttackMaxDeltaY;

        float dx = enemyCenter.x - attackCenter.x;
        float dy = enemyCenter.y - attackCenter.y;
        float ceiling = _sensor.GetCeilingClearanceAt(startPos);

        if (isClose) {
            var closeSol = ClothMotor.ComputeLaunch(
                dx, dy,
                ClothCombatHandler.CloseAttackSpeedX,
                ClothCombatHandler.CloseDiveGravityMultiplier,
                ceiling,
                ClothCombatHandler.CloseJumpHeight,
                ClothCombatHandler.CloseApexTime,
                facing);
            return Simulate(startPos, closeSol, enemy, facing).ReachedEnemy;
        }

        float speed = ClothCombatHandler.AttackMaxSpeedX;
        float dive = ClothCombatHandler.DiveGravityMultiplier;
        var first = ClothMotor.ComputeLaunch(dx, dy, speed, dive, ceiling, -1f, -1f, facing);
        BuildHeightVariants(first.JumpHeight);

        for (int i = 0; i < _variants.Count; i++) {
            float ov = _variants[i];
            var sol = ov < 0f ? first : ClothMotor.ComputeLaunch(dx, dy, speed, dive, ceiling, ov, -1f, facing);
            if (Simulate(startPos, sol, enemy, facing).ReachedEnemy) {
                height = ov;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Plan de ataque: salto de ataque directo si la hitbox llega al enemigo sin chocar; si no,
    /// un salto corto a un punto desde el que sí llegue.
    /// </summary>
    public bool TryPlanAttack(GameObject enemy, Vector2 myFeet, out AttackPlan plan) {
        plan = default;
        plan.Height = -1f;
        plan.Apex = -1f;

        if (TryAttackVariant(_transform.position, enemy, out float h, out float a)) {
            plan.Height = h;
            plan.Apex = a;
            return true;
        }

        if (!Settings.HopPlanningEnabled)
            return false;

        BuildHopCandidates(myFeet);
        for (int i = 0; i < _candidates.Count; i++) {
            Vector2 candidate = _candidates[i];
            if (!ValidateHop(myFeet, candidate))
                continue;

            if (TryAttackVariant(_sensor.FeetToPosition(candidate), enemy, out h, out a)) {
                plan.NeedsHop = true;
                plan.HopFeet = candidate;
                plan.Height = h;
                plan.Apex = a;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Punto de suelo a distancia de seguridad del enemigo (preferiblemente del lado de Cloth) donde aproximarse
    /// o emerger sin quedar bajo/dentro del enemigo.
    /// </summary>
    public bool TryFindApproachPoint(Vector2 enemyCenter, Vector2 myFeet, out Vector2 feet) {
        feet = myFeet;

        float standoff = Mathf.Max(2f, Mathf.Min(Settings.ApproachStandoff, ClothCombatHandler.AttackRange * 0.8f));
        float enemyGroundY = ClothCompanionManager.TryFindGround(enemyCenter, out Vector2 enemyGround, 40f)
            ? enemyGround.y
            : enemyCenter.y - 1f;

        float mySide = myFeet.x >= enemyCenter.x ? 1f : -1f;
        float[] extras = { 0f, 3f, 6f, -3f };

        for (int s = 0; s < 2; s++) {
            float side = s == 0 ? mySide : -mySide;
            foreach (float extra in extras) {
                float x = enemyCenter.x + (side * Mathf.Max(2f, standoff + extra));
                if (!ClothCompanionManager.TryFindGround(new Vector2(x, enemyGroundY + 2f), out Vector2 ground, 8f))
                    continue;
                if (Mathf.Abs(ground.y - enemyGroundY) > 3f)
                    continue;
                if (_sensor.IsBlockedAt(_sensor.FeetToPosition(ground)))
                    continue;

                feet = ground;
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Retreat & Dodge

    /// <summary>
    /// Busca un punto de aterrizaje táctico seguro para evadir una amenaza o proyectil entrante.
    /// Toma en cuenta el tamaño del collider del enemigo y descarta cualquier posición que se encuentre
    /// en su línea de fuego o trayectoria de colisión.
    /// </summary>
    public bool TryFindDodgeLanding(
        Vector2 myFeet,
        GameObject threat,
        Vector2 threatVel,
        ClothTrajectoryPredictor predictor,
        out Vector2 landingFeet,
        out float requiredHeight,
        out float requiredApex) {

        landingFeet = myFeet;
        requiredHeight = Settings.CloseJumpHeight;
        requiredApex = Settings.CloseApexTime;

        if (threat == null) return false;

        var col = ClothSpatialSensor.GetBodyCollider(threat);
        float threatWidth = col != null ? col.bounds.size.x : 2.5f;
        float threatHeight = col != null ? col.bounds.size.y : 3.0f;
        Vector2 threatCenter = ClothSpatialSensor.EnemyCenter(threat);
        Vector2 threatExtents = new(threatWidth * 0.5f, threatHeight * 0.5f);

        float clearance = Mathf.Max(1.0f, Settings.DodgeClearanceMargin);
        float minDodgeDist = Mathf.Max(Settings.DodgeMinDistance, threatWidth + _motor.BodyRadius + clearance);
        Vector2 startPos = _sensor.FeetToPosition(myFeet);
        float ceiling = _sensor.GetCeilingClearanceAt(startPos);

        // Candidatos de evasión:
        // 1. Saltar DETRÁS de la amenaza (lado opuesto a su vector de velocidad): el enemigo se aleja de este punto
        // 2. Saltar a los laterales / lejos de su vector de avance
        List<(float targetX, float jumpH, float apexT)> candidates = new();

        float velSignX = Mathf.Abs(threatVel.x) > 0.1f ? Mathf.Sign(threatVel.x) : Mathf.Sign(myFeet.x - threatCenter.x);

        // Opción 1: Aterrizar detrás de donde viene el enemigo
        float behindX = threatCenter.x - (velSignX * (threatExtents.x + _motor.BodyRadius + clearance + 1.5f));
        float overHeight = Mathf.Max(Settings.CloseJumpHeight, (threatCenter.y + threatExtents.y + 1.2f) - myFeet.y);
        float overApex = Settings.CloseApexTime * Mathf.Sqrt(Mathf.Clamp(overHeight / Mathf.Max(1f, Settings.CloseJumpHeight), 1f, 2.5f));
        candidates.Add((behindX, overHeight, overApex));

        // Opción 2: Salto hacia el lado opuesto al enemigo si hay espacio suficiente
        float awayFromThreatSign = myFeet.x >= threatCenter.x ? 1f : -1f;
        candidates.Add((myFeet.x + (awayFromThreatSign * minDodgeDist), Settings.CloseJumpHeight, Settings.CloseApexTime));
        candidates.Add((myFeet.x + (awayFromThreatSign * (minDodgeDist + 3.5f)), Settings.EscapeJumpHeight * 0.8f, Settings.EscapeApexTime * 0.8f));

        // Opción 3: Salto en la otra dirección si no está en la línea de fuego
        candidates.Add((myFeet.x - (awayFromThreatSign * minDodgeDist), Settings.CloseJumpHeight, Settings.CloseApexTime));

        for (int i = 0; i < candidates.Count; i++) {
            var (candX, h, a) = candidates[i];

            // Comprobar si el techo permite la altura requerida
            if (h > ceiling - 0.5f) continue;

            Vector2 probe = new(candX, myFeet.y + 2f);
            if (!ClothCompanionManager.TryFindGround(probe, out Vector2 ground, 7f))
                continue;

            if (Mathf.Abs(ground.y - myFeet.y) > 4f)
                continue;

            if (_sensor.IsBlockedAt(_sensor.FeetToPosition(ground)))
                continue;

            // Descartar si el punto está dentro de la línea de fuego o trayectoria de la amenaza
            if (predictor.IsPointInTrajectoryCorridor(ground, threatCenter, threatVel, threatExtents, Settings.ThreatLookaheadTime, clearance))
                continue;

            // Simular el arco del salto
            float dx = ground.x - myFeet.x;
            float dy = ground.y - myFeet.y;
            var sol = ClothMotor.ComputeLaunch(
                dx, dy,
                Settings.CloseAttackSpeedX * 1.2f,
                Mathf.Max(1f, Settings.CloseDiveGravityMultiplier),
                ceiling,
                h,
                a,
                Mathf.Abs(dx) > 0.01f ? Mathf.Sign(dx) : 0f
            );

            var sim = Simulate(startPos, sol, null, 0f);
            if (LandsAt(sim, ground, HopTolerance + 0.5f, HopTolerance + 0.5f)) {
                landingFeet = ground;
                requiredHeight = h;
                requiredApex = a;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Punto de suelo en la dirección opuesta al enemigo donde aterrizar con el salto de retroceso.
    /// Prueba la distancia configurada y luego distancias menores. Con simulate, valida la trayectoria.
    /// </summary>
    public bool TryFindRetreatLanding(Vector2 myFeet, float awaySign, bool simulate, out Vector2 landingFeet) {
        landingFeet = myFeet;

        float away = awaySign >= 0f ? 1f : -1f;
        float distance = Mathf.Max(0.5f, ClothCombatHandler.RetreatDistance);
        Vector2 startPos = _sensor.FeetToPosition(myFeet);
        float ceiling = _sensor.GetCeilingClearanceAt(startPos);
        float[] fractions = { 1f, 0.75f, 0.5f };

        foreach (float fraction in fractions) {
            Vector2 probe = new(myFeet.x + (away * distance * fraction), myFeet.y + 1.5f);
            if (!ClothCompanionManager.TryFindGround(probe, out Vector2 ground, 4f))
                continue;
            if (Mathf.Abs(ground.y - myFeet.y) > 2f)
                continue;
            if (_sensor.IsBlockedAt(_sensor.FeetToPosition(ground)))
                continue;

            if (simulate) {
                float dx = ground.x - myFeet.x;
                float dy = ground.y - myFeet.y;
                var sol = ClothMotor.ComputeLaunch(
                    dx, dy,
                    ClothCombatHandler.RetreatSpeedX,
                    Mathf.Max(1f, ClothCombatHandler.RetreatDiveGravityMultiplier),
                    ceiling,
                    ClothCombatHandler.RetreatJumpHeight,
                    ClothCombatHandler.RetreatApexTime,
                    Mathf.Abs(dx) > 0.01f ? Mathf.Sign(dx) : 0f);

                var sim = Simulate(startPos, sol, null, 0f);
                if (!LandsAt(sim, ground, HopTolerance, HopTolerance))
                    continue;
            } else if (!ClothSpatialSensor.HasLineOfSight(_sensor.BodyCenter(), ground + (Vector2.up * _motor.FeetOffset))) {
                continue;
            }

            landingFeet = ground;
            return true;
        }

        return false;
    }

    #endregion

    #region Escape

    /// <summary>
    /// Busca un aterrizaje válido para el gran salto de escape, preferiblemente alejándose del atacante.
    /// Prueba primero la altura completa y, si el techo o el terreno no lo permiten, alturas menores.
    /// </summary>
    public bool TryPlanEscape(Vector2 myFeet, float awaySign, out Vector2 landingFeet, out float height, out float apex) {
        landingFeet = myFeet;
        height = Settings.EscapeJumpHeight;
        apex = Settings.EscapeApexTime;

        Vector2 startPos = _sensor.FeetToPosition(myFeet);
        float baseHeight = Mathf.Max(1f, Settings.EscapeJumpHeight);
        float ceiling = _sensor.GetCeilingClearanceAt(startPos, baseHeight + 2f);
        float dive = Mathf.Max(1f, Settings.EscapeDiveMultiplier);
        float[] fractions = { 1f, 0.7f, 0.45f };
        float[] heightFactors = { 1f, 0.85f, 0.7f, 0.55f };
        float away = awaySign >= 0f ? 1f : -1f;

        for (int s = 0; s < 2; s++) {
            float side = s == 0 ? away : -away;
            foreach (float fraction in fractions) {
                float x = myFeet.x + (side * Settings.EscapeDistance * fraction);
                if (!ClothCompanionManager.TryFindGround(new Vector2(x, myFeet.y + 3f), out Vector2 ground, 14f))
                    continue;
                if (ground.y - myFeet.y > baseHeight * 0.7f)
                    continue;
                if (_sensor.IsBlockedAt(_sensor.FeetToPosition(ground)))
                    continue;

                float dx = ground.x - myFeet.x;
                float dy = ground.y - myFeet.y;

                foreach (float factor in heightFactors) {
                    float h = baseHeight * factor;
                    // Se conserva la gravedad: al bajar la altura también baja el tiempo al ápice
                    float a = Settings.EscapeApexTime * Mathf.Sqrt(factor);

                    var sol = ClothMotor.ComputeLaunch(
                        dx, dy,
                        Settings.EscapeSpeedX,
                        dive,
                        ceiling,
                        h,
                        a,
                        Mathf.Abs(dx) > 0.01f ? Mathf.Sign(dx) : 0f);

                    var sim = Simulate(startPos, sol, null, 0f);
                    if (LandsAt(sim, ground, Settings.LandingToleranceX, Settings.LandingToleranceY)) {
                        landingFeet = ground;
                        height = h;
                        apex = a;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    #endregion
}
