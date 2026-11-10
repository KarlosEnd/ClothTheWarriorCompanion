using ClothTheWarriorCompanion.Config;

using HeroCtrl = HeroController;

namespace ClothTheWarriorCompanion.Behaviors;

/// <summary>
/// Controlador principal de IA y máquina de comportamiento para la compañera Cloth.
/// Orquesta la navegación, el combate y las secuencias de animación coordinando
/// los subsistemas de detección espacial, movimiento físico y combate.
/// </summary>
public class ClothFollowBehavior : MonoBehaviour {
    #region Tuning & Configuration

    private static ClothSettings Settings => ClothSettings.Current;

    // Accesos directos a parámetros de seguimiento y rescate
    public static float FollowDistanceX => Settings.FollowDistanceX;
    public static float FollowDistanceY => Settings.FollowDistanceY;
    public static float BurrowThresholdX => Settings.BurrowThresholdX;
    public static float BurrowThresholdEnemyX => Settings.BurrowThresholdEnemyX;
    public static float MaxFallDropDeltaY => Settings.MaxFallDropDeltaY;
    public static float FollowCooldownAfterJump => Settings.FollowCooldownAfterJump;
    public static float FollowCooldownAfterBurrow => Settings.FollowCooldownAfterBurrow;
    public static float JumpNormalMaxSpeedX => Settings.JumpNormalMaxSpeedX;
    public static float EnemyScanInterval => Settings.EnemyScanInterval;
    public static float RescueDistanceTotal => Settings.RescueDistanceTotal;
    public static float RescueDistanceY => Settings.RescueDistanceY;
    public static float RescueAbyssY => Settings.RescueAbyssY;
    public static float BusyRescueTimeout => Settings.BusyRescueTimeout;
    public static float AttackCooldown => Settings.AttackCooldown;
    public static float maxAllowedGroundDistance => Settings.maxAllowedGroundDistance;
    public static float JumpFallGravityMultiplier => Settings.JumpFallGravityMultiplier;
    public static float JumpRecoverDurationScale => Settings.JumpRecoverDurationScale;

    #endregion

    private tk2dSpriteAnimator _animator = null!;
    private MeshRenderer? _meshRenderer;
    private tk2dSprite? _sprite;
    private GameObject _hero = null!;

    private ClothMotor _motor = null!;
    private ClothSpatialSensor _sensor = null!;
    private ClothCombatHandler _combat = null!;

    private bool _isBusy = false;
    private float _busyTimer = 0f;
    private float _followCooldown = 0f;
    private float _attackCooldown = 0f;
    private float _retreatCooldown = 0f;

    // Daño recibido
    private int _damageTaken = 0;
    private float _invulnTimer = 0f;
    private float _retargetTimer = 0f;
    private bool _isDowned = false;
    private bool _isBurrowing = false;
    private readonly Collider2D[] _damageBuffer = new Collider2D[64];
    private float _scanTimer = 0f;
    private float _safetyTimer = 0f;

    private GameObject? _currentEnemy = null;

    /// <summary>
    /// Cloth puede ser objetivo de los enemigos salvo si está excavando/emergiendo o derrotada.
    /// </summary>
    public bool IsTargetable => !_isDowned && !_isBurrowing;

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
    }

    private void LateUpdate() {
        // Asegurar que el MeshRenderer permanezca activo
        if (_meshRenderer != null && !_meshRenderer.enabled && !_isDowned) {
            _meshRenderer.enabled = true;
            ClothTheWarriorCompanionMod.Instance?.LogDebug("Cloth MeshRenderer re-enabled in LateUpdate.");
        }
    }

    public void ResetState() {
        StopAllCoroutines();
        _isBusy = false;
        _busyTimer = 0f;
        _followCooldown = 0f;
        _attackCooldown = 0.5f;
        _retreatCooldown = 0f;
        _isBurrowing = false;
        _isDowned = false;

        _motor.ResetPhysics();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Teletransporta a Cloth a la posición de suelo indicada ejecutando la animación de emergencia.
    /// </summary>
    public void AppearAt(Vector2 feetGroundPosition) {
        ResetState();
        _damageTaken = 0;
        StartCoroutine(AppearCoroutine(feetGroundPosition));
    }

    /// <summary>
    /// Fuerza a Cloth a excavar y emerger en la posición objetivo.
    /// </summary>
    public void ForceBurrow(Vector2 feetGroundPosition) {
        ResetState();
        StartCoroutine(BurrowCoroutine(feetGroundPosition));
    }

    private void Rescue() => AppearAt(ClothCompanionManager.GetKnightGroundPosition());

    #endregion

    #region AI & Combat Loop

    private void Update() {
        if (!ClothCompanionManager.FollowPlayerJumping || _hero == null)
            return;

        var gameManager = GameManager.instance;
        if (gameManager == null || gameManager.gameState != GlobalEnums.GameState.PLAYING)
            return;

        if (_invulnTimer > 0f)
            _invulnTimer -= Time.deltaTime;
        CheckIncomingDamage();

        // Hace que los enemigos cercanos puedan elegir a Cloth como objetivo
        _retargetTimer -= Time.deltaTime;
        if (_retargetTimer <= 0f) {
            _retargetTimer = 0.25f;
            Hooks.EnemyTargetRedirector.Retarget(gameObject, IsTargetable);
        }

        if (_isBusy) {
            _busyTimer += Time.deltaTime;
            if (_busyTimer > BusyRescueTimeout)
                Rescue();
            return;
        }
        _busyTimer = 0f;

        Vector2 heroGround = ClothCompanionManager.GetKnightGroundPosition();
        Vector2 currentFeet = _sensor.Feet();

        //Validamos si el héroe está tocando el suelo o si tiene un piso muy cerca (max 8f)
        bool heroIsSafe = HeroCtrl.instance.cState.onGround || Mathf.Abs(heroGround.y - _sensor.HeroCenter().y) <= maxAllowedGroundDistance;

        // Rescate por desincronización de distancia o piso vertical
        if (heroIsSafe && (Vector2.Distance(currentFeet, heroGround) > RescueDistanceTotal || Mathf.Abs(heroGround.y - currentFeet.y) > RescueDistanceY)) {
            Rescue();
            return;
        }

        // Detección de atascamiento o caída al abismo
        _safetyTimer -= Time.deltaTime;
        if (_safetyTimer <= 0f) {
            _safetyTimer = 0.25f;
            if (_sensor.IsEmbedded() || transform.position.y < heroGround.y - RescueAbyssY) {
                Rescue();
                return;
            }
        }

        if (_followCooldown > 0f)
            _followCooldown -= Time.deltaTime;
        if (_attackCooldown > 0f)
            _attackCooldown -= Time.deltaTime;
        if (_retreatCooldown > 0f)
            _retreatCooldown -= Time.deltaTime;

        _scanTimer -= Time.deltaTime;
        if (_scanTimer <= 0f) {
            _scanTimer = EnemyScanInterval;
            _currentEnemy = _combat.FindBestEnemy(_sensor.BodyCenter(), _sensor.HeroCenter(), gameObject);
        }

        // Reflejo cuerpo a cuerpo: cualquier enemigo que toque su hitbox pasa a ser el objetivo
        if (_attackCooldown <= 0f) {
            var touching = _combat.FindEnemyInHitbox(transform, gameObject);
            if (touching != null)
                _currentEnemy = touching;
        }

        Vector2 myCenter = _sensor.BodyCenter();

        if (_currentEnemy != null && _combat.IsValidTarget(_currentEnemy, gameObject)) {
            Vector2 enemyCenter = ClothSpatialSensor.EnemyCenter(_currentEnemy);

            // La distancia de ataque se mide desde el centro de la hitbox (orientada hacia el enemigo)
            float facingToEnemy = enemyCenter.x >= myCenter.x ? 1f : -1f;
            Vector2 attackCenter = ClothCombatHandler.GetAttackBoxCenter(transform.position, facingToEnemy);

            float deltaX = Mathf.Abs(enemyCenter.x - attackCenter.x);
            float deltaY = enemyCenter.y - attackCenter.y;
            bool hasLos = ClothSpatialSensor.HasLineOfSight(myCenter, enemyCenter);
            float maxReach = _sensor.GetMaxReach();

            if (hasLos && deltaX <= ClothCombatHandler.AttackRange && deltaY <= maxReach) {
                // Si el enemigo está pegado, retrocede con un salto sin golpe para reposicionarse; si no, ataca
                if (_attackCooldown <= 0f && !TryStartRetreat(_currentEnemy, myCenter)) {
                    StartCoroutine(AttackCoroutine(_currentEnemy));
                }
            } else if (hasLos && deltaX <= ClothCombatHandler.AttackAnimDistance && deltaY > maxReach) {
                // El enemigo está demasiado alto; pausar sin realizar burrow indiscriminado
            } else if (_followCooldown <= 0f) {
                Vector2 enemyGround = ClothCompanionManager.TryFindGround(enemyCenter, out Vector2 groundPoint, 40f) ? groundPoint : heroGround;
                MoveToTarget(enemyGround, isEnemyTarget: true);
            }
        } else {
            bool isFarX = Mathf.Abs(heroGround.x - currentFeet.x) > FollowDistanceX;
            bool isFarY = Mathf.Abs(heroGround.y - currentFeet.y) > FollowDistanceY;
            if ((isFarX || isFarY) && _followCooldown <= 0f && heroIsSafe) {
                MoveToTarget(heroGround, isEnemyTarget: false);
            }
        }
    }

    private void MoveToTarget(Vector2 targetFeet, bool isEnemyTarget) {
        Vector2 myFeet = _sensor.Feet();
        float deltaX = targetFeet.x - myFeet.x;
        float deltaY = targetFeet.y - myFeet.y;
        float maxXThreshold = isEnemyTarget ? BurrowThresholdEnemyX : BurrowThresholdX;

        bool isPathBlocked = !ClothSpatialSensor.HasLineOfSight(_sensor.BodyCenter(), targetFeet + (Vector2.up * _motor.FeetOffset));

        if (isPathBlocked || Mathf.Abs(deltaX) > maxXThreshold || deltaY > _sensor.GetMaxReach() || deltaY < MaxFallDropDeltaY) {
            StartCoroutine(BurrowCoroutine(targetFeet));
        } else {
            StartCoroutine(JumpCoroutine(deltaX, deltaY));
        }
    }

    #endregion

    #region Coroutines

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

    private IEnumerator JumpCoroutine(float distX, float distY) {
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
            out float apexTime, out float totalFlightTime, out float launchGravityScale);
        _animator.Play("Jump");

        bool isDiving = false;
        float elapsed = 0f;
        while (elapsed < totalFlightTime + 2.5f) {
            elapsed += Time.deltaTime;

            // Caída más rápida tras el ápice
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

    /// <summary>
    /// Reproduce "Jump Attack Recover" desde el frame indicado (0 = inicio) y espera su duración escalada.
    /// </summary>
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

    /// <summary>
    /// Duración de los frames de "Jump" que se muestran tras el golpe antes de pasar a la animación de caída.
    /// </summary>
    private float GetPostAttackJumpDuration() {
        var clip = _animator.GetClipByName("Jump");
        float fps = clip != null && clip.fps > 0 ? clip.fps : 12f;
        return Mathf.Max(0, ClothCombatHandler.PostAttackJumpFrames) / fps;
    }

    /// <summary>
    /// Aplica el daño con la hitbox de ataque en su posición actual (invertida según la orientación).
    /// </summary>
    private void DoSlam(float facing) {
        _combat.ExecuteAttackBoxDamage(
            gameObject,
            ClothCombatHandler.GetAttackBoxCenter(transform.position, facing),
            ClothCombatHandler.AttackBoxSize,
            ClothCombatHandler.DefaultSlamDamageAmount);
    }

    private IEnumerator AttackCoroutine(GameObject enemyTarget) {
        _isBusy = true;

        // La orientación se fija al iniciar el ataque y NO cambia hasta que termine
        Vector2 targetCenter = ClothSpatialSensor.EnemyCenter(enemyTarget);
        _motor.TurnToFace(targetCenter.x - transform.position.x);
        float facing = ClothCombatHandler.GetFacing(transform);

        // El tipo de salto se decide antes del antic (cada uno tiene su duración)
        Vector2 attackCenter = ClothCombatHandler.GetAttackBoxCenter(transform.position, facing);
        bool isClose = ClothCombatHandler.CloseAttackDistance > 0f
            && Vector2.Distance(attackCenter, targetCenter) <= ClothCombatHandler.CloseAttackDistance
            && Mathf.Abs(targetCenter.y - attackCenter.y) <= ClothCombatHandler.CloseAttackMaxDeltaY;

        // Parámetros según el tipo de ataque
        float anticDuration = isClose ? ClothCombatHandler.CloseAnticDuration : ClothCombatHandler.AttackAnticDuration;
        float animLeadTime = isClose ? ClothCombatHandler.CloseAnimLeadTime : ClothCombatHandler.AttackAnimLeadTime;
        float animDistance = isClose ? ClothCombatHandler.CloseAnimDistance : ClothCombatHandler.AttackAnimDistance;
        float airHitDistance = isClose ? ClothCombatHandler.CloseAirHitDistance : ClothCombatHandler.AirHitDistance;
        float landCheckDelay = isClose ? ClothCombatHandler.CloseLandCheckDelay : ClothCombatHandler.AttackLandCheckDelay;
        float diveMultiplier = isClose ? ClothCombatHandler.CloseDiveGravityMultiplier : ClothCombatHandler.DiveGravityMultiplier;
        float recoverScale = isClose ? ClothCombatHandler.CloseRecoverDurationScale : ClothCombatHandler.RecoverDurationScale;
        float cooldownAfter = isClose ? ClothCombatHandler.CloseAttackCooldown : AttackCooldown;

        _animator.Play("Jump Attack Antic");
        yield return new WaitForSeconds(Mathf.Max(0f, anticDuration));

        if (!_combat.IsValidTarget(enemyTarget, gameObject)) {
            _animator.Play("Idle");
            _attackCooldown = 0.4f;
            _isBusy = false;
            yield break;
        }

        // Recalcular con la posición actual: el salto apunta a que el CENTRO DE LA HITBOX llegue al enemigo
        targetCenter = ClothSpatialSensor.EnemyCenter(enemyTarget);
        attackCenter = ClothCombatHandler.GetAttackBoxCenter(transform.position, facing);
        float distX = targetCenter.x - attackCenter.x;
        float distY = targetCenter.y - attackCenter.y;

        float apexTime, totalFlightTime, launchGravityScale;
        bool hasSlammed = false;
        bool hasPlayedAttackAnim = false;
        bool isDiving = false;

        _motor.BeginAirborne();

        if (isClose) {
            // Salto corto: se mantiene "Jump Attack Antic" mientras sube; sin animación "Jump"
            _motor.Launch(
                distX, distY,
                ClothCombatHandler.CloseAttackSpeedX,
                diveMultiplier,
                _sensor.GetCeilingClearance(),
                out apexTime, out totalFlightTime, out launchGravityScale,
                ClothCombatHandler.CloseJumpHeight,
                ClothCombatHandler.CloseApexTime,
                facing);
        } else {
            _motor.Launch(
                distX, distY,
                ClothCombatHandler.AttackMaxSpeedX,
                diveMultiplier,
                _sensor.GetCeilingClearance(),
                out apexTime, out totalFlightTime, out launchGravityScale,
                -1f, -1f,
                facing);

            _animator.Play("Jump");
        }

        float elapsed = 0f;
        float slamElapsed = 0f;
        // Caída tras el golpe: 0 = aún no, 1 = "Jump" unos frames, 2 = "Jump Attack Antic" como caída
        int fallPhase = 0;
        float fallPhaseStart = 0f;
        float jumpPhaseDuration = 0f;

        while (elapsed < totalFlightTime + 2.5f) {
            elapsed += Time.deltaTime;

            // Mantener la orientación fijada durante todo el ataque
            _motor.TurnToFace(facing);

            if (!isDiving && elapsed >= apexTime) {
                _motor.SetGravityScale(launchGravityScale * diveMultiplier);
                isDiving = true;
            }

            Vector2 currentEnemyPosition = _combat.IsValidTarget(enemyTarget, gameObject) ? ClothSpatialSensor.EnemyCenter(enemyTarget) : targetCenter;
            Vector2 currentAttackCenter = ClothCombatHandler.GetAttackBoxCenter(transform.position, facing);
            float distanceToEnemy = Vector2.Distance(currentAttackCenter, currentEnemyPosition);
            bool isDescending = _motor.VelocityY < 0f;

            // "Jump Attack" solo se reproduce al descender (por distancia o por tiempo restante de vuelo)
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
                DoSlam(facing);
                hasSlammed = true;
                slamElapsed = elapsed;
            }

            if (isLanded)
                break;

            // Tras el golpe (haya conectado o no), si sigue cayendo: "Jump" unos frames y luego
            // "Jump Attack Antic" como animación de caída hasta tocar el suelo.
            if (hasSlammed) {
                if (fallPhase == 0 && elapsed - slamElapsed >= ClothCombatHandler.PostAttackFallDelay) {
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
            DoSlam(facing);
        }

        _motor.EndAirborne();

        // Si hubo caída tras el golpe, el Recover arranca desde el frame configurado; si no, completo
        int recoverStartFrame = fallPhase > 0 ? Mathf.Max(0, ClothCombatHandler.PostAttackFallRecoverStartFrame - 1) : 0;
        yield return PlayRecover(recoverScale, recoverStartFrame);

        _animator.Play("Idle");

        _attackCooldown = cooldownAfter;
        _isBusy = false;
    }

    /// <summary>
    /// Si el enemigo está pegado a Cloth, inicia un salto de retroceso sin golpe. Devuelve true si lo inició.
    /// </summary>
    private bool TryStartRetreat(GameObject enemyTarget, Vector2 myCenter) {
        float trigger = ClothCombatHandler.RetreatTriggerDistance;
        if (trigger <= 0f || _retreatCooldown > 0f || ClothCombatHandler.IsPlayer(enemyTarget))
            return false;

        Vector2 gap = ClothSpatialSensor.GetGapToTarget(enemyTarget, myCenter);
        if (gap.x > trigger || gap.y > ClothCombatHandler.RetreatMaxDeltaY)
            return false;

        Vector2 enemyCenter = ClothSpatialSensor.EnemyCenter(enemyTarget);
        float towardEnemy = enemyCenter.x >= myCenter.x ? 1f : -1f;
        float awayDirection = -towardEnemy;

        // El punto de aterrizaje debe ser suelo libre y alcanzable; si no (pared, abismo), mejor atacar
        Vector2 myFeet = _sensor.Feet();
        Vector2 probe = new(myFeet.x + (awayDirection * Mathf.Max(0.5f, ClothCombatHandler.RetreatDistance)), myFeet.y + 1f);
        if (!ClothCompanionManager.TryFindGround(probe, out Vector2 landingFeet, 4f))
            return false;
        if (Mathf.Abs(landingFeet.y - myFeet.y) > 2f)
            return false;
        if (_sensor.IsBlockedAt(_sensor.FeetToPosition(landingFeet)))
            return false;
        if (!ClothSpatialSensor.HasLineOfSight(myCenter, landingFeet + (Vector2.up * _motor.FeetOffset)))
            return false;

        StartCoroutine(RetreatCoroutine(towardEnemy, landingFeet));
        return true;
    }

    /// <summary>
    /// Salto hacia atrás sin golpe: "Jump Attack Antic" durante todo el vuelo (también al caer),
    /// "Jump Attack Recover" al tocar el suelo y luego Idle. Cloth sigue mirando al enemigo.
    /// </summary>
    private IEnumerator RetreatCoroutine(float facing, Vector2 landingFeet) {
        _isBusy = true;
        _motor.TurnToFace(facing);

        _animator.Play("Jump Attack Antic");
        yield return new WaitForSeconds(Mathf.Max(0f, ClothCombatHandler.RetreatAnticDuration));

        Vector2 myFeet = _sensor.Feet();
        float distX = landingFeet.x - myFeet.x;
        float distY = landingFeet.y - myFeet.y;
        float diveMultiplier = Mathf.Max(1f, ClothCombatHandler.RetreatDiveGravityMultiplier);

        _motor.BeginAirborne();
        _motor.Launch(
            distX, distY,
            ClothCombatHandler.RetreatSpeedX,
            diveMultiplier,
            _sensor.GetCeilingClearance(),
            out float apexTime, out float totalFlightTime, out float launchGravityScale,
            ClothCombatHandler.RetreatJumpHeight,
            ClothCombatHandler.RetreatApexTime,
            Mathf.Abs(distX) > 0.01f ? Mathf.Sign(distX) : 0f);

        bool isDiving = false;
        float elapsed = 0f;
        while (elapsed < totalFlightTime + 2.5f) {
            elapsed += Time.deltaTime;
            _motor.TurnToFace(facing);

            if (!isDiving && elapsed >= apexTime) {
                _motor.SetGravityScale(launchGravityScale * diveMultiplier);
                isDiving = true;
            }

            if (elapsed > ClothCombatHandler.RetreatLandCheckDelay && _motor.VelocityY <= 0.1f && _sensor.IsGrounded())
                break;
            yield return null;
        }

        _motor.EndAirborne();

        yield return PlayRecover(ClothCombatHandler.RetreatRecoverDurationScale, 0);
        _animator.Play("Idle");

        _retreatCooldown = ClothCombatHandler.RetreatCooldown;
        _attackCooldown = 0.1f;
        _isBusy = false;
    }

    #endregion

    #region Damage Received

    /// <summary>
    /// Detecta ataques y contacto de enemigos: cualquier DamageHero activo que solape con el cuerpo de Cloth.
    /// (Los DamageHero solo reaccionan a la capa del Caballero, por eso se consulta el solape manualmente.)
    /// </summary>
    private void CheckIncomingDamage() {
        if (!Settings.CanTakeDamage || _invulnTimer > 0f || !IsTargetable)
            return;

        Vector2 center = _sensor.BodyCenter();
        Vector2 size = _sensor.BodySize() * 0.9f;
        int count = Physics2D.OverlapBoxNonAlloc(center, size, 0f, _damageBuffer, ~ClothCompanionManager.TerrainMask);

        for (int i = 0; i < count; i++) {
            var col = _damageBuffer[i];
            if (col == null || col.transform.IsChildOf(transform))
                continue;

            var damageHero = col.GetComponent<DamageHero>();
            if (damageHero == null || !damageHero.isActiveAndEnabled || damageHero.damageDealt <= 0)
                continue;

            // Peligros del entorno (pinchos, ácido...) no cuentan; solo enemigos
            if (damageHero.hazardType > 1)
                continue;

            var owner = col.GetComponentInParent<HealthManager>();
            Vector2 sourcePosition = owner != null ? owner.transform.position : col.transform.position;

            ReceiveHit(sourcePosition, damageHero.damageDealt);
            return;
        }
    }

    private void ReceiveHit(Vector2 sourcePosition, int damage) {
        _damageTaken += Mathf.Max(1, damage);
        _invulnTimer = Mathf.Max(0f, Settings.HurtInvulnerability);
        bool defeated = Settings.MaxHealth > 0 && _damageTaken >= Settings.MaxHealth;

        float dx = _sensor.BodyCenter().x - sourcePosition.x;
        float direction = Mathf.Abs(dx) > 0.05f ? Mathf.Sign(dx) : -ClothCombatHandler.GetFacing(transform);

        ClothTheWarriorCompanionMod.Instance?.LogDebug($"Cloth recibió {damage} de daño ({_damageTaken}/{Settings.MaxHealth}).");

        ResetState();
        StartCoroutine(defeated ? DefeatedCoroutine(direction) : HurtCoroutine(direction));
    }

    private void PlayHurtAnimation() {
        float fps = Settings.HurtAnimFps;
        if (fps <= 0f)
            return; // animación de daño desactivada

        var clip = _animator.GetClipByName("Jump Attack Antic");
        if (clip != null)
            _animator.Play(clip, 0f, fps);
        else
            _animator.Play("Jump Attack Antic");
    }

    /// <summary>
    /// Impulso de knockback (alejándose del atacante) con la animación de daño; termina al tocar suelo.
    /// </summary>
    private IEnumerator KnockbackRoutine(float direction) {
        float speedX = Settings.KnockbackSpeedX;
        _motor.ApplyKnockback(new Vector2(direction * speedX, Settings.KnockbackSpeedY));
        PlayHurtAnimation();

        float duration = Mathf.Max(0.05f, Settings.HurtStunDuration);
        float elapsed = 0f;
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            _motor.SetVelocityX(direction * speedX * (1f - Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        _motor.SetVelocityX(0f);

        float airTime = 0f;
        while (airTime < 1.5f && !(_motor.VelocityY <= 0.1f && _sensor.IsGrounded())) {
            airTime += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator HurtCoroutine(float direction) {
        _isBusy = true;
        _motor.TurnToFace(-direction); // mira hacia el atacante

        yield return KnockbackRoutine(direction);

        _animator.Play("Idle");
        _attackCooldown = 0.3f;
        _isBusy = false;
    }

    /// <summary>
    /// Sin vida: recibe el golpe, se hunde, permanece fuera un tiempo y reaparece junto al Caballero con la vida completa.
    /// </summary>
    private IEnumerator DefeatedCoroutine(float direction) {
        _isBusy = true;
        _isDowned = true;
        _motor.TurnToFace(-direction);

        yield return KnockbackRoutine(direction);

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

        if (_meshRenderer != null)
            _meshRenderer.enabled = false;

        float wait = Mathf.Max(0f, Settings.DefeatedReturnDelay);
        float elapsed = 0f;
        while (elapsed < wait) {
            elapsed += Time.deltaTime;
            _busyTimer = 0f; // evita el rescate por atasco mientras está fuera de combate
            yield return null;
        }

        _damageTaken = 0;
        _isDowned = false;
        yield return AppearCoroutine(ClothCompanionManager.GetKnightGroundPosition());
    }

    #endregion
}
