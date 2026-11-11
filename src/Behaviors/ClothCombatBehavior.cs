using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ClothTheWarriorCompanion.Config;

namespace ClothTheWarriorCompanion.Behaviors;

/// <summary>
/// Subsistema de combate inteligente para Cloth.
/// Coordina la selección táctica de objetivos múltiples, la anticipación y evasión de amenazas entrantes,
/// el cálculo de intercepción balística ofensiva y la ejecución de ataques en corto o largo alcance.
/// </summary>
internal sealed class ClothCombatBehavior {
    private static ClothSettings Settings => ClothSettings.Current;

    private readonly MonoBehaviour _host;
    private readonly tk2dSpriteAnimator _animator;
    private readonly ClothMotor _motor;
    private readonly ClothSpatialSensor _sensor;
    private readonly ClothCombatHandler _combat;
    private readonly ClothJumpPlanner _planner;
    private readonly ClothTrajectoryPredictor _predictor;
    private readonly ClothTargetSelector _targetSelector;

    private bool _isBusy = false;
    private float _attackCooldown = 0f;
    private float _retreatCooldown = 0f;
    private float _planRetryTimer = 0f;

    public bool IsBusy => _isBusy;
    public float AttackCooldown => _attackCooldown;
    public void SetAttackCooldown(float cd) => _attackCooldown = cd;

    public ClothCombatBehavior(
        MonoBehaviour host,
        tk2dSpriteAnimator animator,
        ClothMotor motor,
        ClothSpatialSensor sensor,
        ClothCombatHandler combat,
        ClothJumpPlanner planner,
        ClothTrajectoryPredictor predictor,
        ClothTargetSelector targetSelector) {

        _host = host;
        _animator = animator;
        _motor = motor;
        _sensor = sensor;
        _combat = combat;
        _planner = planner;
        _predictor = predictor;
        _targetSelector = targetSelector;
    }

    public void ResetState() {
        _isBusy = false;
        _attackCooldown = 0.4f;
        _retreatCooldown = 0f;
        _planRetryTimer = 0f;
    }

    public void UpdateTimers() {
        if (_attackCooldown > 0f) _attackCooldown -= Time.deltaTime;
        if (_retreatCooldown > 0f) _retreatCooldown -= Time.deltaTime;
        if (_planRetryTimer > 0f) _planRetryTimer -= Time.deltaTime;
    }

    /// <summary>
    /// Bucle principal de combate. Evalúa amenazas entrantes y objetivos, decidiendo ataques o evasiones.
    /// Retorna true si ejecutó una acción o está ocupado en combate; false si Cloth debe continuar navegando hacia el Caballero.
    /// </summary>
    public bool UpdateCombatLoop(Vector2 heroGround, Vector2 currentFeet, Action<Vector2> approachEnemyCallback) {
        if (_isBusy) return true;

        UpdateTimers();

        Vector2 myCenter = _sensor.BodyCenter();
        Vector2 heroCenter = _sensor.HeroCenter();
        float bodyRadius = _motor.BodyRadius;

        // 1. Actualizar escaneo y priorización de objetivos múltiples
        _targetSelector.UpdateTargets(
            myCenter,
            bodyRadius,
            heroCenter,
            _host.gameObject,
            _predictor,
            _combat,
            _attackCooldown,
            ClothCombatHandler.AttackAnticDuration
        );

        // 2. Reflejo cuerpo a cuerpo: si un enemigo está tocando su hitbox actual, forzarlo como objetivo
        if (_attackCooldown <= 0f) {
            var touching = _combat.FindEnemyInHitbox(_host.transform, _host.gameObject);
            if (touching != null) {
                _predictor.Track(touching, ClothSpatialSensor.EnemyCenter(touching));
                _host.StartCoroutine(AttackCoroutine(touching, forceClose: true));
                return true;
            }
        }

        // 3. ANÁLISIS DEFENSIVO: ¿Existe una amenaza inminente que va a colisionar con Cloth?
        if (_targetSelector.HasImminentThreat(out RankedTarget threatTarget)) {
            if (threatTarget.Threat.CanCounterInTime) {
                // Cloth tiene tiempo para atacar primero e interceptar la embestida enemiga
                ClothTheWarriorCompanionMod.Instance?.LogDebug($"Intercepción ofensiva a amenaza '{threatTarget.Enemy.name}' (TTC: {threatTarget.Threat.TimeToCollision:0.00}s).");
                _host.GetComponent<ClothAudioController>()?.PlayBattleCry();
                _host.StartCoroutine(AttackCoroutine(threatTarget.Enemy));
                return true;
            }

            if (threatTarget.Threat.ShouldDodge) {
                // No alcanza a atacar a tiempo: evadir antes de recibir el impacto fuera de la línea de fuego
                ClothTheWarriorCompanionMod.Instance?.LogDebug($"Esquiva táctica ante amenaza '{threatTarget.Enemy.name}' (TTC: {threatTarget.Threat.TimeToCollision:0.00}s).");
                if (TryDodgeThreat(threatTarget, currentFeet)) {
                    return true;
                }
            }
        }

        // 4. ACCIÓN OFENSIVA CON EL OBJETIVO LÍDER
        var best = _targetSelector.BestTarget;
        if (best == null || !_combat.IsValidTarget(best.Value.Enemy, _host.gameObject)) {
            return false;
        }

        GameObject currentEnemy = best.Value.Enemy;
        Vector2 enemyCenter = ClothSpatialSensor.EnemyCenter(currentEnemy);
        float facingToEnemy = enemyCenter.x >= myCenter.x ? 1f : -1f;
        Vector2 attackCenter = ClothCombatHandler.GetAttackBoxCenter(_host.transform.position, facingToEnemy);

        float deltaX = Mathf.Abs(enemyCenter.x - attackCenter.x);
        float deltaY = enemyCenter.y - attackCenter.y;
        float maxReach = _sensor.GetMaxReach();
        bool usePlanner = Settings.UseJumpPlanner;
        bool inRange = (best.Value.HasLineOfSight || usePlanner) && deltaX <= ClothCombatHandler.AttackRange && deltaY <= maxReach;

        if (inRange) {
            // Si el enemigo está pegado o encima de Cloth, retroceder con un salto corto
            if (TryStartRetreat(currentEnemy, myCenter)) {
                return true;
            }

            if (_attackCooldown <= 0f) {
                if (!usePlanner) {
                    _host.StartCoroutine(AttackCoroutine(currentEnemy));
                    return true;
                }

                if (_planRetryTimer <= 0f) {
                    _planRetryTimer = Mathf.Max(0.05f, Settings.JumpPlanRetryInterval);
                    if (_planner.TryPlanAttack(currentEnemy, currentFeet, out AttackPlan attackPlan)) {
                        _host.StartCoroutine(attackPlan.NeedsHop
                            ? HopThenAttackCoroutine(currentEnemy, attackPlan)
                            : AttackCoroutine(currentEnemy, attackPlan.Height, attackPlan.Apex));
                        return true;
                    }

                    approachEnemyCallback(enemyCenter);
                    return true;
                }
            }
            return true;
        }

        if (best.Value.HasLineOfSight && deltaX <= ClothCombatHandler.AttackAnimDistance && deltaY > maxReach) {
            // El objetivo está demasiado alto; pausar temporalmente sin excavar sin motivo
            return true;
        }

        // Fuera de rango de ataque: aproximarse al enemigo
        approachEnemyCallback(enemyCenter);
        return true;
    }

    /// <summary>
    /// Intenta esquivar una amenaza inminente buscando una posición de aterrizaje que no esté
    /// en la línea de fuego del enemigo y considerando el tamaño real de su collider.
    /// </summary>
    private bool TryDodgeThreat(RankedTarget threat, Vector2 currentFeet) {
        // 1. Probar buscador táctico de evasión (salto detrás del enemigo o a los laterales fuera del cono de peligro)
        if (Settings.ThreatDodgeHopEnabled || Settings.ThreatDodgeJumpEnabled) {
            if (_planner.TryFindDodgeLanding(currentFeet, threat.Enemy, threat.Threat.EnemyVelocity, _predictor, out Vector2 dodgeLanding, out float dodgeHeight, out float dodgeApex)) {
                _host.GetComponent<ClothAudioController>()?.PlayJump();
                _host.StartCoroutine(DodgeJumpCoroutine(dodgeLanding, dodgeHeight, dodgeApex, Mathf.Sign(dodgeLanding.x - currentFeet.x), threat.Enemy));
                return true;
            }
        }

        // 2. Fallback: escape direccional opuesto al vector de avance
        float awayDir = threat.Threat.EnemyVelocity.x != 0f
            ? -Mathf.Sign(threat.Threat.EnemyVelocity.x)
            : (currentFeet.x >= ClothSpatialSensor.EnemyCenter(threat.Enemy).x ? 1f : -1f);

        if (Settings.ThreatDodgeJumpEnabled) {
            if (_planner.TryPlanEscape(currentFeet, awayDir, out Vector2 landing, out float escapeHeight, out float escapeApex)) {
                _host.GetComponent<ClothAudioController>()?.PlayJump();
                _host.StartCoroutine(DodgeJumpCoroutine(landing, escapeHeight, escapeApex, awayDir, threat.Enemy));
                return true;
            }
        }

        return false;
    }

    private IEnumerator DodgeHopCoroutine(Vector2 landingFeet, GameObject enemy, float awayDir) {
        _isBusy = true;
        float facing = -awayDir; // Mirando hacia la amenaza mientras retrocede

        _host.GetComponent<ClothAudioController>()?.PlayJump();
        yield return ExecuteHopRoutine(
            facing,
            landingFeet,
            ClothCombatHandler.RetreatJumpHeight,
            ClothCombatHandler.RetreatApexTime,
            ClothCombatHandler.RetreatSpeedX,
            ClothCombatHandler.RetreatDiveGravityMultiplier,
            ClothCombatHandler.RetreatAnticDuration * 0.7f,
            ClothCombatHandler.RetreatLandCheckDelay,
            ClothCombatHandler.RetreatRecoverDurationScale,
            playJumpAnimation: false,
            endWithIdle: false
        );

        _retreatCooldown = ClothCombatHandler.RetreatCooldown;

        // Al tocar tierra, contraatacar inmediatamente si el enemigo sigue en combate
        if (_combat.IsValidTarget(enemy, _host.gameObject)) {
            yield return AttackCoroutine(enemy, -1f, -1f, forceClose: true);
            yield break;
        }

        _animator.Play("Idle");
        _attackCooldown = 0.15f;
        _isBusy = false;
    }

    private IEnumerator DodgeJumpCoroutine(Vector2 landingFeet, float height, float apex, float awayDir, GameObject? threatEnemy = null) {
        _isBusy = true;
        _motor.TurnToFace(awayDir);

        _host.GetComponent<ClothAudioController>()?.PlayJump();
        yield return ExecuteHopRoutine(
            awayDir,
            landingFeet,
            height,
            apex,
            Settings.EscapeSpeedX,
            Settings.EscapeDiveMultiplier,
            Settings.EscapeAnticDuration,
            0.2f,
            Settings.JumpRecoverDurationScale,
            playJumpAnimation: true,
            endWithIdle: false
        );

        _retreatCooldown = ClothCombatHandler.RetreatCooldown;

        // Si la amenaza es un enemigo válido y no un proyectil desvanecido, contraatacar si está al alcance
        if (threatEnemy != null && _combat.IsValidTarget(threatEnemy, _host.gameObject)) {
            Vector2 enemyCenter = ClothSpatialSensor.EnemyCenter(threatEnemy);
            float dist = Vector2.Distance(_sensor.BodyCenter(), enemyCenter);
            if (dist <= ClothCombatHandler.AttackRange) {
                yield return AttackCoroutine(threatEnemy, -1f, -1f, forceClose: dist <= ClothCombatHandler.CloseAttackDistance);
                yield break;
            }
        }

        _animator.Play("Idle");
        _attackCooldown = 0.35f;
        _isBusy = false;
    }

    public bool TryStartRetreat(GameObject enemyTarget, Vector2 myCenter) {
        float trigger = ClothCombatHandler.RetreatTriggerDistance;
        if (trigger <= 0f || _retreatCooldown > 0f || ClothCombatHandler.IsPlayer(enemyTarget))
            return false;

        Vector2 gap = ClothSpatialSensor.GetGapToTarget(enemyTarget, myCenter);
        if (gap.x > trigger || gap.y > ClothCombatHandler.RetreatMaxDeltaY)
            return false;

        Vector2 enemyCenter = ClothSpatialSensor.EnemyCenter(enemyTarget);
        float towardEnemy = enemyCenter.x >= myCenter.x ? 1f : -1f;

        if (Mathf.Abs(enemyCenter.x - myCenter.x) < 0.5f) {
            towardEnemy = _sensor.HeroCenter().x >= myCenter.x ? -1f : 1f;
        }

        if (!_planner.TryFindRetreatLanding(_sensor.Feet(), -towardEnemy, Settings.UseJumpPlanner, out Vector2 landingFeet)) {
            _retreatCooldown = 0.25f;
            return false;
        }

        _host.StartCoroutine(RetreatCoroutine(towardEnemy, landingFeet, enemyTarget));
        return true;
    }

    private IEnumerator RetreatCoroutine(float facing, Vector2 landingFeet, GameObject? enemyTarget = null) {
        _isBusy = true;
        bool chainAttack = Settings.RetreatChainsAttack && enemyTarget != null;

        yield return ExecuteHopRoutine(
            facing, landingFeet,
            ClothCombatHandler.RetreatJumpHeight,
            ClothCombatHandler.RetreatApexTime,
            ClothCombatHandler.RetreatSpeedX,
            ClothCombatHandler.RetreatDiveGravityMultiplier,
            ClothCombatHandler.RetreatAnticDuration,
            ClothCombatHandler.RetreatLandCheckDelay,
            ClothCombatHandler.RetreatRecoverDurationScale,
            endWithIdle: !chainAttack
        );

        _retreatCooldown = ClothCombatHandler.RetreatCooldown;

        if (chainAttack && _combat.IsValidTarget(enemyTarget!, _host.gameObject)) {
            yield return AttackCoroutine(enemyTarget!, -1f, -1f, forceClose: true);
            yield break;
        }

        if (chainAttack) _animator.Play("Idle");

        _attackCooldown = 0.1f;
        _isBusy = false;
    }

    private IEnumerator HopThenAttackCoroutine(GameObject enemyTarget, AttackPlan plan) {
        _isBusy = true;

        Vector2 landing = plan.HopFeet;
        float dir = landing.x - _sensor.Feet().x;
        float facing = Mathf.Abs(dir) > 0.01f ? Mathf.Sign(dir) : ClothCombatHandler.GetFacing(_host.transform);

        yield return ExecuteHopRoutine(
            facing, landing,
            Settings.CloseJumpHeight,
            Settings.CloseApexTime,
            Settings.CloseAttackSpeedX,
            Settings.CloseDiveGravityMultiplier,
            Settings.CloseAnticDuration,
            Settings.CloseLandCheckDelay,
            Settings.CloseRecoverDurationScale
        );

        if (!_combat.IsValidTarget(enemyTarget, _host.gameObject)) {
            _animator.Play("Idle");
            _attackCooldown = 0.3f;
            _isBusy = false;
            yield break;
        }

        yield return AttackCoroutine(enemyTarget, plan.Height, plan.Apex);
    }

    /// <summary>
    /// Corutina central de ataque con salto de Cloth.
    /// Resuelve cinemática balística predictiva para interceptar objetivos en movimiento, asegurando que
    /// la hitbox de impacto conecte limpiamente sin colisionar el cuerpo antes de tiempo.
    /// </summary>
    public IEnumerator AttackCoroutine(GameObject enemyTarget, float heightOverride = -1f, float apexOverride = -1f, bool forceClose = false) {
        _isBusy = true;

        Vector2 originalCenter = ClothSpatialSensor.EnemyCenter(enemyTarget);
        _motor.TurnToFace(originalCenter.x - _host.transform.position.x);
        float facing = ClothCombatHandler.GetFacing(_host.transform);

        Vector2 attackCenter = ClothCombatHandler.GetAttackBoxCenter(_host.transform.position, facing);
        bool isClose = forceClose || (ClothCombatHandler.CloseAttackDistance > 0f
            && Vector2.Distance(attackCenter, originalCenter) <= ClothCombatHandler.CloseAttackDistance
            && Mathf.Abs(originalCenter.y - attackCenter.y) <= ClothCombatHandler.CloseAttackMaxDeltaY);

        float anticDuration = isClose ? ClothCombatHandler.CloseAnticDuration : ClothCombatHandler.AttackAnticDuration;
        float animLeadTime = isClose ? ClothCombatHandler.CloseAnimLeadTime : ClothCombatHandler.AttackAnimLeadTime;
        float animDistance = isClose ? ClothCombatHandler.CloseAnimDistance : ClothCombatHandler.AttackAnimDistance;
        float airHitDistance = isClose ? ClothCombatHandler.CloseAirHitDistance : ClothCombatHandler.AirHitDistance;
        float landCheckDelay = isClose ? ClothCombatHandler.CloseLandCheckDelay : ClothCombatHandler.AttackLandCheckDelay;
        float diveMultiplier = isClose ? ClothCombatHandler.CloseDiveGravityMultiplier : ClothCombatHandler.DiveGravityMultiplier;
        float recoverScale = isClose ? ClothCombatHandler.CloseRecoverDurationScale : ClothCombatHandler.RecoverDurationScale;
        float cooldownAfter = isClose ? ClothCombatHandler.CloseAttackCooldown : Settings.AttackCooldown;
        int slamDamage = isClose ? ClothCombatHandler.CloseSlamDamageAmount : ClothCombatHandler.DefaultSlamDamageAmount;

        _animator.Play("Jump Attack Antic");
        yield return new WaitForSeconds(Mathf.Max(0f, anticDuration));

        if (!_combat.IsValidTarget(enemyTarget, _host.gameObject)) {
            _animator.Play("Idle");
            _attackCooldown = 0.4f;
            _isBusy = false;
            yield break;
        }

        // PREDICCIÓN AVANZADA: Cálculo balístico de intercepción considerando dimensiones de collider y hitbox
        float maxSpeedX = isClose ? ClothCombatHandler.CloseAttackSpeedX : ClothCombatHandler.AttackMaxSpeedX;
        float jumpH = isClose ? ClothCombatHandler.CloseJumpHeight : heightOverride;
        float apexT = isClose ? ClothCombatHandler.CloseApexTime : apexOverride;
        float clearance = _sensor.GetCeilingClearance();

        bool hasSolution = _predictor.TryCalculateAttackIntercept(
            _host.transform.position,
            _motor.BodyRadius,
            enemyTarget,
            maxSpeedX,
            diveMultiplier,
            clearance,
            jumpH,
            apexT,
            out InterceptSolution intercept
        );

        float distX, distY, apexTime, totalFlightTime, launchGravityScale;
        Vector2 targetCenter;

        if (hasSolution) {
            distX = intercept.DistX;
            distY = intercept.DistY;
            apexTime = intercept.Launch.ApexTime;
            totalFlightTime = intercept.Launch.TotalFlightTime;
            launchGravityScale = intercept.Launch.GravityScale;
            targetCenter = intercept.PredictedTargetPos;
            facing = intercept.Facing;
        } else {
            targetCenter = _predictor.PredictPosition(enemyTarget, isClose ? ClothCombatHandler.CloseApexTime * 2f : 0.65f);
            attackCenter = ClothCombatHandler.GetAttackBoxCenter(_host.transform.position, facing);
            distX = targetCenter.x - attackCenter.x;
            distY = targetCenter.y - attackCenter.y;
            var fallbackSol = ClothMotor.ComputeLaunch(distX, distY, maxSpeedX, diveMultiplier, clearance, jumpH, apexT, facing);
            apexTime = fallbackSol.ApexTime;
            totalFlightTime = fallbackSol.TotalFlightTime;
            launchGravityScale = fallbackSol.GravityScale;
        }

        _motor.BeginAirborne();
        _host.GetComponent<ClothAudioController>()?.PlayAttack();

        if (hasSolution) {
            _motor.ApplySolution(intercept.Launch, intercept.Launch.VelocityX);
            if (!isClose) {
                _animator.Play("Jump");
            }
        } else {
            _motor.Launch(
                distX, distY,
                maxSpeedX,
                diveMultiplier,
                clearance,
                out apexTime, out totalFlightTime, out launchGravityScale,
                jumpH, apexT,
                facing
            );
            if (!isClose) {
                _animator.Play("Jump");
            }
        }

        bool hasSlammed = false;
        bool hasPlayedAttackAnim = false;
        bool isDiving = false;
        float elapsed = 0f;
        float slamElapsed = 0f;
        int fallPhase = 0;
        float fallPhaseStart = 0f;
        float jumpPhaseDuration = 0f;

        while (elapsed < totalFlightTime + 2.5f) {
            elapsed += Time.deltaTime;
            _motor.TurnToFace(facing);

            if (!isDiving && elapsed >= apexTime) {
                _motor.SetGravityScale(launchGravityScale * diveMultiplier);
                isDiving = true;
            }

            Vector2 currentEnemyPosition = _combat.IsValidTarget(enemyTarget, _host.gameObject)
                ? ClothSpatialSensor.EnemyCenter(enemyTarget)
                : targetCenter;
            Vector2 currentAttackCenter = ClothCombatHandler.GetAttackBoxCenter(_host.transform.position, facing);
            float distanceToEnemy = Vector2.Distance(currentAttackCenter, currentEnemyPosition);
            bool isDescending = _motor.VelocityY < 0f;

            if (!hasPlayedAttackAnim && isDescending && (distanceToEnemy <= animDistance || elapsed >= totalFlightTime - animLeadTime)) {
                _animator.Play("Jump Attack");
                hasPlayedAttackAnim = true;
            }

            bool isLanded = elapsed > landCheckDelay && _motor.VelocityY <= 0.1f && _sensor.IsGrounded();

            if (!hasSlammed && (isLanded || elapsed >= totalFlightTime || (hasPlayedAttackAnim && isDescending && distanceToEnemy <= airHitDistance))) {
                if (!hasPlayedAttackAnim) {
                    _animator.Play("Jump Attack");
                    hasPlayedAttackAnim = true;
                }
                DoSlam(facing, slamDamage);
                hasSlammed = true;
                slamElapsed = elapsed;
            }

            if (hasSlammed && elapsed - slamElapsed <= Settings.AttackActiveDuration) {
                DoSlam(facing, slamDamage);
            }

            if (isLanded) break;

            if (hasSlammed) {
                if (fallPhase == 0 && elapsed - slamElapsed >= Settings.AttackActiveDuration + ClothCombatHandler.PostAttackFallDelay) {
                    _animator.Play("Jump");
                    jumpPhaseDuration = GetPostAttackJumpDuration();
                    fallPhaseStart = elapsed;
                    fallPhase = 1;
                } else if (fallPhase == 1 && elapsed - fallPhaseStart >= jumpPhaseDuration) {
                    _animator.Play("Jump Attack Antic");
                    fallPhase = 2;
                }
            }

            yield return null;
        }

        if (!hasSlammed) {
            DoSlam(facing, slamDamage);
        }

        _motor.EndAirborne();

        int recoverStartFrame = fallPhase > 0 ? Mathf.Max(0, ClothCombatHandler.PostAttackFallRecoverStartFrame - 1) : 0;
        yield return PlayRecover(recoverScale, recoverStartFrame);

        _animator.Play("Idle");
        _attackCooldown = cooldownAfter;
        _isBusy = false;
    }

    /// <summary>
    /// Salto genérico reutilizable (retroceso, posicionamiento y esquiva).
    /// </summary>
    public IEnumerator ExecuteHopRoutine(
        float facing,
        Vector2 landingFeet,
        float height,
        float apex,
        float speedX,
        float dive,
        float anticDuration,
        float landCheckDelay,
        float recoverScale,
        bool playJumpAnimation = false,
        bool endWithIdle = true) {

        _motor.TurnToFace(facing);

        _animator.Play("Jump Attack Antic");
        yield return new WaitForSeconds(Mathf.Max(0f, anticDuration));

        Vector2 myFeet = _sensor.Feet();
        float distX = landingFeet.x - myFeet.x;
        float distY = landingFeet.y - myFeet.y;
        float diveMultiplier = Mathf.Max(1f, dive);

        _motor.BeginAirborne();
        _host.GetComponent<ClothAudioController>()?.PlayJump();
        _motor.Launch(
            distX, distY,
            speedX,
            diveMultiplier,
            _sensor.GetCeilingClearanceAt(_host.transform.position, Mathf.Max(12f, height + 2f)),
            out float apexTime, out float totalFlightTime, out float launchGravityScale,
            height,
            apex,
            Mathf.Abs(distX) > 0.01f ? Mathf.Sign(distX) : 0f
        );

        if (playJumpAnimation) {
            _animator.Play("Jump");
        }

        bool isDiving = false;
        float elapsed = 0f;
        while (elapsed < totalFlightTime + 2.5f) {
            elapsed += Time.deltaTime;
            _motor.TurnToFace(facing);

            if (!isDiving && elapsed >= apexTime) {
                _motor.SetGravityScale(launchGravityScale * diveMultiplier);
                isDiving = true;
            }

            if (elapsed > landCheckDelay && _motor.VelocityY <= 0.1f && _sensor.IsGrounded())
                break;
            yield return null;
        }

        _motor.EndAirborne();

        yield return PlayRecover(recoverScale, 0);
        if (endWithIdle) {
            _animator.Play("Idle");
        }
    }

    private void DoSlam(float facing, int damage) {
        _combat.ExecuteAttackBoxDamage(
            _host.gameObject,
            ClothCombatHandler.GetAttackBoxCenter(_host.transform.position, facing),
            ClothCombatHandler.AttackBoxSize,
            damage
        );
    }

    private float GetPostAttackJumpDuration() {
        var clip = _animator.GetClipByName("Jump");
        float fps = clip != null && clip.fps > 0 ? clip.fps : 12f;
        return Mathf.Max(0, ClothCombatHandler.PostAttackJumpFrames) / fps;
    }

    private IEnumerator PlayRecover(float durationScale, int startFrame) {
        var clip = _animator.GetClipByName("Jump Attack Recover");
        if (clip == null || clip.frames == null || clip.frames.Length == 0) {
            _animator.Play("Jump Attack Recover");
            yield return new WaitForSeconds(Mathf.Max(0f, 0.4f * durationScale));
            yield break;
        }

        int totalFrames = clip.frames.Length;
        int start = Mathf.Clamp(startFrame, 0, totalFrames - 1);
        float fps = clip.fps > 0 ? clip.fps : 12f;

        _animator.PlayFromFrame(clip, start);
        yield return new WaitForSeconds(Mathf.Max(0f, (totalFrames - start) / fps * durationScale));
    }
}

