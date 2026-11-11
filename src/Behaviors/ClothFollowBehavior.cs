using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ClothTheWarriorCompanion.Config;
using HeroCtrl = HeroController;

namespace ClothTheWarriorCompanion.Behaviors;

/// <summary>
/// Controlador principal de IA y máquina de comportamiento para la compañera Cloth.
/// Orquesta la navegación, el seguimiento del Caballero y el ciclo de vida coordinando
/// los subsistemas modulares de detección espacial, combate táctico, recepción de daño y física cinemática.
/// </summary>
public class ClothFollowBehavior : MonoBehaviour {
    #region Tuning & Configuration

    private static ClothSettings Settings => ClothSettings.Current;

    public static float FollowDistanceX => Settings.FollowDistanceX;
    public static float FollowDistanceY => Settings.FollowDistanceY;
    public static float BurrowThresholdX => Settings.BurrowThresholdX;
    public static float BurrowThresholdEnemyX => Settings.BurrowThresholdEnemyX;
    public static float MaxFallDropDeltaY => Settings.MaxFallDropDeltaY;
    public static float FollowCooldownAfterJump => Settings.FollowCooldownAfterJump;
    public static float FollowCooldownAfterBurrow => Settings.FollowCooldownAfterBurrow;
    public static float JumpNormalMaxSpeedX => Settings.JumpNormalMaxSpeedX;
    public static float RescueDistanceTotal => Settings.RescueDistanceTotal;
    public static float RescueDistanceY => Settings.RescueDistanceY;
    public static float RescueAbyssY => Settings.RescueAbyssY;
    public static float BusyRescueTimeout => Settings.BusyRescueTimeout;
    public static float maxAllowedGroundDistance => Settings.maxAllowedGroundDistance;
    public static float JumpFallGravityMultiplier => Settings.JumpFallGravityMultiplier;
    public static float JumpRecoverDurationScale => Settings.JumpRecoverDurationScale;

    #endregion

    private tk2dSpriteAnimator _animator = null!;
    private MeshRenderer? _meshRenderer;
    private tk2dSprite? _sprite;
    private GameObject _hero = null!;

    // Subsistemas modulares
    private ClothMotor _motor = null!;
    private ClothSpatialSensor _sensor = null!;
    private ClothCombatHandler _combat = null!;
    private ClothJumpPlanner _planner = null!;
    private ClothTrajectoryPredictor _predictor = null!;
    private ClothTargetSelector _targetSelector = null!;
    private ClothCombatBehavior _combatBehavior = null!;
    private ClothDamageReceiver _damageReceiver = null!;

    private bool _isBusy = false;
    private float _busyTimer = 0f;
    private float _followCooldown = 0f;
    private float _retargetTimer = 0f;
    private float _safetyTimer = 0f;
    private bool _isBurrowing = false;

    /// <summary>
    /// Cloth puede ser objetivo de los enemigos salvo si está excavando/emergiendo o derrotada.
    /// </summary>
    public bool IsTargetable => !_damageReceiver.IsDowned && !_isBurrowing;

    /// <summary>
    /// Indica si Cloth se encuentra ejecutando alguna acción prioritaria en cualquiera de sus subsistemas.
    /// </summary>
    public bool IsBusy => _isBusy || _damageReceiver.IsBusy || _combatBehavior.IsBusy;

    #region Lifecycle

    private void Awake() {
        _animator = GetComponent<tk2dSpriteAnimator>();
        _meshRenderer = GetComponent<MeshRenderer>();
        _sprite = GetComponent<tk2dSprite>();

        var rb = GetComponent<Rigidbody2D>();
        var normalCollider = GetComponent<BoxCollider2D>();
        _hero = HeroCtrl.instance.gameObject;
        var heroCollider = _hero.GetComponent<BoxCollider2D>();

        _motor = new ClothMotor(transform, rb, normalCollider);
        _sensor = new ClothSpatialSensor(transform, normalCollider, _hero, heroCollider, _motor.FeetOffset);
        _combat = new ClothCombatHandler();
        _planner = new ClothJumpPlanner(transform, _motor, _sensor);
        _predictor = new ClothTrajectoryPredictor();
        _targetSelector = new ClothTargetSelector();

        _combatBehavior = new ClothCombatBehavior(
            this,
            _animator,
            _motor,
            _sensor,
            _combat,
            _planner,
            _predictor,
            _targetSelector
        );

        _damageReceiver = new ClothDamageReceiver(
            this,
            _animator,
            _motor,
            _sensor,
            _combat,
            _planner,
            _predictor,
            () => _combatBehavior,
            AppearAt
        );
    }

    private void LateUpdate() {
        if (_meshRenderer != null && !_meshRenderer.enabled && !_damageReceiver.IsDowned) {
            _meshRenderer.enabled = true;
            ClothTheWarriorCompanionMod.Instance?.LogDebug("Cloth MeshRenderer re-enabled in LateUpdate.");
        }
    }

    public void ResetState() {
        StopAllCoroutines();
        _isBusy = false;
        _busyTimer = 0f;
        _followCooldown = 0f;
        _isBurrowing = false;

        _damageReceiver.ResetState();
        _combatBehavior.ResetState();
        _motor.ResetPhysics();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Teletransporta a Cloth a la posición de suelo indicada ejecutando la animación de emergencia.
    /// </summary>
    public void AppearAt(Vector2 feetGroundPosition) {
        ResetState();
        _damageReceiver.FullReset();
        StartCoroutine(AppearCoroutine(feetGroundPosition));
    }

    /// <summary>
    /// Fuerza a Cloth a excavar y emerger en la posición objetivo.
    /// </summary>
    public void ForceBurrow(Vector2 feetGroundPosition) {
        ResetState();
        StartCoroutine(BurrowCoroutine(feetGroundPosition));
    }

    /// <summary>
    /// Pasa el golpe del Caballero al receptor de daño de Cloth.
    /// </summary>
    public void ReceiveKnightHit() {
        _damageReceiver.ReceiveKnightHit();
    }

    /// <summary>
    /// Recibe un impacto nativo de Hollow Knight desde ClothHitResponder (clavo, artes del clavo, hechizos).
    /// </summary>
    public void HandleHitInstance(HitInstance hitInstance) {
        _damageReceiver.ReceiveHitInstance(hitInstance);
    }

    private void Rescue() => AppearAt(ClothCompanionManager.GetKnightGroundPosition());

    #endregion

    #region AI & Navigation Loop

    private void Update() {
        if (!ClothCompanionManager.FollowPlayerJumping || _hero == null)
            return;

        var gameManager = GameManager.instance;
        if (gameManager == null || gameManager.gameState != GlobalEnums.GameState.PLAYING)
            return;

        // Actualizar temporizadores y detección de daño recibido
        _damageReceiver.UpdateTimers(IsTargetable);

        // Hace que los enemigos cercanos puedan elegir a Cloth como objetivo
        _retargetTimer -= Time.deltaTime;
        if (_retargetTimer <= 0f) {
            _retargetTimer = Mathf.Max(0.05f, Settings.EnemyRetargetInterval);
            Hooks.EnemyTargetRedirector.Retarget(gameObject, IsTargetable);
        }

        if (IsBusy) {
            _busyTimer += Time.deltaTime;
            if (_busyTimer > BusyRescueTimeout) {
                Rescue();
            }
            return;
        }
        _busyTimer = 0f;

        Vector2 heroGround = ClothCompanionManager.GetKnightGroundPosition();
        Vector2 currentFeet = _sensor.Feet();

        // Validamos si el héroe está tocando el suelo o si tiene un piso muy cerca
        bool heroIsSafe = HeroCtrl.instance.cState.onGround || Mathf.Abs(heroGround.y - _sensor.HeroCenter().y) <= maxAllowedGroundDistance;

        // Rescate por desincronización de distancia o piso vertical
        if (heroIsSafe && (Vector2.Distance(currentFeet, heroGround) > RescueDistanceTotal || Mathf.Abs(heroGround.y - currentFeet.y) > RescueDistanceY)) {
            Rescue();
            return;
        }

        // Limpieza de historiales de enemigos y detección de atascamiento o caída al abismo
        _safetyTimer -= Time.deltaTime;
        if (_safetyTimer <= 0f) {
            _safetyTimer = 0.25f;
            _predictor.CleanUp(k => _combat.IsValidTarget(k, gameObject));
            if (_sensor.IsEmbedded() || transform.position.y < heroGround.y - RescueAbyssY) {
                Rescue();
                return;
            }
        }

        if (_followCooldown > 0f)
            _followCooldown -= Time.deltaTime;

        // EJECUTAR BUCLE DE COMBATE MODULAR:
        // Evalúa objetivos múltiples, amenazas entrantes por TTC/CPA y ataques de intercepción.
        bool combatHandled = _combatBehavior.UpdateCombatLoop(
            heroGround,
            currentFeet,
            enemyCenter => {
                if (_followCooldown <= 0f) {
                    ApproachEnemy(enemyCenter, currentFeet, heroGround);
                }
            }
        );

        if (combatHandled)
            return;

        // SEGUIMIENTO DEL CABALLERO (FUERA DE COMBATE):
        bool isFarX = Mathf.Abs(heroGround.x - currentFeet.x) > FollowDistanceX;
        bool isFarY = Mathf.Abs(heroGround.y - currentFeet.y) > FollowDistanceY;
        if ((isFarX || isFarY) && _followCooldown <= 0f && heroIsSafe) {
            MoveToTarget(heroGround, isEnemyTarget: false);
        }
    }

    /// <summary>
    /// Se aproxima a un enemigo buscando un punto seguro de suelo para no emerger dentro de él.
    /// </summary>
    private void ApproachEnemy(Vector2 enemyCenter, Vector2 myFeet, Vector2 heroGround) {
        Vector2 target;
        if (Settings.UseJumpPlanner) {
            if (_planner.TryFindApproachPoint(enemyCenter, myFeet, out target)) {
                if (Mathf.Abs(target.x - myFeet.x) < 2f && Mathf.Abs(target.y - myFeet.y) < 2f) {
                    _followCooldown = 0.4f;
                    return;
                }
            } else {
                target = heroGround;
            }
        } else {
            target = ClothCompanionManager.TryFindGround(enemyCenter, out Vector2 groundPoint, 40f) ? groundPoint : heroGround;
        }

        MoveToTarget(target, isEnemyTarget: true);
    }

    private void MoveToTarget(Vector2 targetFeet, bool isEnemyTarget) {
        Vector2 myFeet = _sensor.Feet();
        float deltaX = targetFeet.x - myFeet.x;
        float deltaY = targetFeet.y - myFeet.y;
        float maxXThreshold = isEnemyTarget ? BurrowThresholdEnemyX : BurrowThresholdX;

        bool outOfRange = Mathf.Abs(deltaX) > maxXThreshold || deltaY > _sensor.GetMaxReach() || deltaY < MaxFallDropDeltaY;

        if (Settings.UseJumpPlanner) {
            if (!outOfRange && _planner.TryPlanTravel(myFeet, targetFeet, out TravelPlan plan)) {
                StartCoroutine(TravelCoroutine(plan, targetFeet));
            } else {
                StartCoroutine(BurrowCoroutine(targetFeet));
            }
            return;
        }

        bool isPathBlocked = !ClothSpatialSensor.HasLineOfSight(_sensor.BodyCenter(), targetFeet + (Vector2.up * _motor.FeetOffset));

        if (isPathBlocked || outOfRange) {
            StartCoroutine(BurrowCoroutine(targetFeet));
        } else {
            StartCoroutine(JumpCoroutine(deltaX, deltaY));
        }
    }

    #endregion

    #region Navigation Coroutines

    private IEnumerator AppearCoroutine(Vector2 feetGroundPosition) {
        _isBusy = true;
        _isBurrowing = true;

        Vector3 resolvedPosition = _sensor.ResolveSafeSpot(feetGroundPosition);
        transform.position = resolvedPosition;
        _motor.Rigidbody.position = resolvedPosition;
        Physics2D.SyncTransforms();

        if (_meshRenderer != null)
            _meshRenderer.enabled = true;
        if (_sprite != null) {
            Color color = _sprite.color;
            color.a = 1f;
            _sprite.color = color;
        }

        _motor.PrepareForBurrowOrAppear();

        var clip = _animator.GetClipByName("Emerge");
        if (clip != null) {
            _animator.Play(clip);
            float duration = clip.frames.Length / (clip.fps > 0 ? clip.fps : 15f);
            yield return new WaitForSeconds(duration);
        } else {
            _animator.Play("Emerge");
            yield return new WaitForSeconds(1.0f);
        }

        _animator.Play("Idle");
        _motor.CompleteAppear();

        _followCooldown = FollowCooldownAfterJump;
        _isBurrowing = false;
        _isBusy = false;
    }

    private IEnumerator BurrowCoroutine(Vector2 targetFeetPosition) {
        _isBusy = true;
        _isBurrowing = true;

        _motor.PrepareForBurrowOrAppear();

        var clip = _animator.GetClipByName("Emerge");
        if (clip != null) {
            _animator.Play(clip);
            _animator.Stop();
            float frameTime = 1f / (clip.fps > 0 ? clip.fps : 15f);
            for (int i = clip.frames.Length - 1; i >= 0; i--) {
                _animator.SetFrame(i);
                yield return new WaitForSeconds(frameTime);
            }
        }

        yield return AppearCoroutine(targetFeetPosition);
        _followCooldown = FollowCooldownAfterBurrow;
    }

    private IEnumerator TravelCoroutine(TravelPlan plan, Vector2 targetFeet) {
        _isBusy = true;

        float height = plan.Height;
        float apex = plan.Apex;

        if (plan.NeedsHop) {
            yield return PositioningHop(plan.HopFeet);

            if (!_planner.TryFindTravelVariant(_sensor.Feet(), targetFeet, out height, out apex)) {
                yield return BurrowCoroutine(targetFeet);
                yield break;
            }
        }

        Vector2 feet = _sensor.Feet();
        yield return JumpCoroutine(targetFeet.x - feet.x, targetFeet.y - feet.y, height, apex);
    }

    private IEnumerator PositioningHop(Vector2 landingFeet) {
        float dir = landingFeet.x - _sensor.Feet().x;
        float facing = Mathf.Abs(dir) > 0.01f ? Mathf.Sign(dir) : ClothCombatHandler.GetFacing(transform);

        yield return _combatBehavior.ExecuteHopRoutine(
            facing, landingFeet,
            Settings.CloseJumpHeight,
            Settings.CloseApexTime,
            Settings.CloseAttackSpeedX,
            Settings.CloseDiveGravityMultiplier,
            Settings.CloseAnticDuration,
            Settings.CloseLandCheckDelay,
            Settings.CloseRecoverDurationScale
        );
    }

    private IEnumerator JumpCoroutine(float distX, float distY, float heightOverride = -1f, float apexOverride = -1f) {
        _isBusy = true;
        _motor.TurnToFace(distX);

        _animator.Play("Jump Attack Antic");
        yield return new WaitForSeconds(0.1f);

        float diveMultiplier = Mathf.Max(1f, JumpFallGravityMultiplier);

        _motor.BeginAirborne();
        _motor.Launch(
            distX, distY,
            JumpNormalMaxSpeedX,
            diveMultiplier,
            _sensor.GetCeilingClearance(),
            out float apexTime, out float totalFlightTime, out float launchGravityScale,
            heightOverride, apexOverride
        );
        _animator.Play("Jump");

        bool isDiving = false;
        float elapsed = 0f;
        while (elapsed < totalFlightTime + 2.5f) {
            elapsed += Time.deltaTime;

            if (!isDiving && elapsed >= apexTime) {
                _motor.SetGravityScale(launchGravityScale * diveMultiplier);
                isDiving = true;
            }

            if (elapsed > 0.25f && _motor.VelocityY <= 0.1f && _sensor.IsGrounded())
                break;
            yield return null;
        }

        _motor.EndAirborne();

        yield return PlayRecover(JumpRecoverDurationScale, 0);
        _animator.Play("Idle");

        _followCooldown = FollowCooldownAfterJump;
        _isBusy = false;
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

    #endregion
}
