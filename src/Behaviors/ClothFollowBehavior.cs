using HeroCtrl = HeroController;

namespace ClothTheWarriorCompanion.Behaviors;

/// <summary>
/// Controlador principal de IA y máquina de comportamiento para la compañera Cloth.
/// Orquesta la navegación, el combate y las secuencias de animación coordinando
/// los subsistemas de detección espacial, movimiento físico y combate.
/// </summary>
public class ClothFollowBehavior : MonoBehaviour {
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
    private float _scanTimer = 0f;
    private float _safetyTimer = 0f;

    private GameObject? _currentEnemy = null;

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
        if (_meshRenderer != null && !_meshRenderer.enabled) {
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

        _motor.ResetPhysics();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Teletransporta a Cloth a la posición de suelo indicada ejecutando la animación de emergencia.
    /// </summary>
    public void AppearAt(Vector2 feetGroundPosition) {
        ResetState();
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

        if (_isBusy) {
            _busyTimer += Time.deltaTime;
            if (_busyTimer > 8f)
                Rescue();
            return;
        }
        _busyTimer = 0f;

        Vector2 heroGround = ClothCompanionManager.GetKnightGroundPosition();
        Vector2 currentFeet = _sensor.Feet();

        // Rescate por desincronización de distancia o piso vertical
        if (Vector2.Distance(currentFeet, heroGround) > 45f || Mathf.Abs(heroGround.y - currentFeet.y) > 30f) {
            Rescue();
            return;
        }

        // Detección de atascamiento o caída al abismo
        _safetyTimer -= Time.deltaTime;
        if (_safetyTimer <= 0f) {
            _safetyTimer = 0.25f;
            if (_sensor.IsEmbedded() || transform.position.y < heroGround.y - 40f) {
                Rescue();
                return;
            }
        }

        if (_followCooldown > 0f)
            _followCooldown -= Time.deltaTime;
        if (_attackCooldown > 0f)
            _attackCooldown -= Time.deltaTime;

        _scanTimer -= Time.deltaTime;
        if (_scanTimer <= 0f) {
            _scanTimer = 0.4f;
            _currentEnemy = _combat.FindBestEnemy(_sensor.BodyCenter(), _sensor.HeroCenter(), gameObject);
        }

        Vector2 myCenter = _sensor.BodyCenter();

        if (_currentEnemy != null && _combat.IsValidTarget(_currentEnemy, gameObject)) {
            Vector2 enemyCenter = ClothSpatialSensor.EnemyCenter(_currentEnemy);
            float deltaX = Mathf.Abs(enemyCenter.x - myCenter.x);
            float deltaY = enemyCenter.y - myCenter.y;
            bool hasLos = ClothSpatialSensor.HasLineOfSight(myCenter, enemyCenter);
            float maxReach = _sensor.GetMaxReach();

            if (hasLos && deltaX <= ClothCombatHandler.AttackRange && deltaY <= maxReach) {
                if (_attackCooldown <= 0f) {
                    StartCoroutine(AttackCoroutine(_currentEnemy));
                }
            } else if (hasLos && deltaX <= 6f && deltaY > maxReach) {
                // El enemigo está demasiado alto; pausar sin realizar burrow indiscriminado
            } else if (_followCooldown <= 0f) {
                Vector2 enemyGround = ClothCompanionManager.TryFindGround(enemyCenter, out Vector2 groundPoint, 40f) ? groundPoint : heroGround;
                MoveToTarget(enemyGround, isEnemyTarget: true);
            }
        } else {
            bool isFarX = Mathf.Abs(heroGround.x - currentFeet.x) > 7f;
            bool isFarY = Mathf.Abs(heroGround.y - currentFeet.y) > 3f;
            if ((isFarX || isFarY) && _followCooldown <= 0f) {
                MoveToTarget(heroGround, isEnemyTarget: false);
            }
        }
    }

    private void MoveToTarget(Vector2 targetFeet, bool isEnemyTarget) {
        Vector2 myFeet = _sensor.Feet();
        float deltaX = targetFeet.x - myFeet.x;
        float deltaY = targetFeet.y - myFeet.y;
        float maxXThreshold = isEnemyTarget ? 30f : 28f;

        bool isPathBlocked = !ClothSpatialSensor.HasLineOfSight(_sensor.BodyCenter(), targetFeet + (Vector2.up * _motor.FeetOffset));

        if (isPathBlocked || Mathf.Abs(deltaX) > maxXThreshold || deltaY > _sensor.GetMaxReach() || deltaY < -14f) {
            StartCoroutine(BurrowCoroutine(targetFeet));
        } else {
            StartCoroutine(JumpCoroutine(deltaX, deltaY));
        }
    }

    #endregion

    #region Coroutines

    private IEnumerator AppearCoroutine(Vector2 feetGroundPosition) {
        _isBusy = true;

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

        _followCooldown = 0.5f;
        _isBusy = false;
    }

    private IEnumerator BurrowCoroutine(Vector2 targetFeetPosition) {
        _isBusy = true;

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
        _followCooldown = 1.0f;
    }

    private IEnumerator JumpCoroutine(float distX, float distY) {
        _isBusy = true;
        _motor.TurnToFace(distX);

        _animator.Play("Jump Attack Antic");
        yield return new WaitForSeconds(0.1f);

        _motor.BeginAirborne();
        _motor.Launch(distX, distY, 20f, 1f, _sensor.GetCeilingClearance(), out _, out float totalFlightTime, out _);
        _animator.Play("Jump");

        float elapsed = 0f;
        while (elapsed < totalFlightTime + 2.5f) {
            elapsed += Time.deltaTime;
            if (elapsed > 0.25f && _motor.VelocityY <= 0.1f && _sensor.IsGrounded())
                break;
            yield return null;
        }

        _motor.EndAirborne();
        _animator.Play("Idle");

        _followCooldown = 0.5f;
        _isBusy = false;
    }

    private IEnumerator AttackCoroutine(GameObject enemyTarget) {
        _isBusy = true;

        Vector2 targetCenter = ClothSpatialSensor.EnemyCenter(enemyTarget);
        _motor.TurnToFace(targetCenter.x - transform.position.x);

        _animator.Play("Jump Attack Antic");
        yield return new WaitForSeconds(0.1f);

        if (!_combat.IsValidTarget(enemyTarget, gameObject)) {
            _animator.Play("Idle");
            _attackCooldown = 0.4f;
            _isBusy = false;
            yield break;
        }

        targetCenter = ClothSpatialSensor.EnemyCenter(enemyTarget);
        Vector2 currentCenter = _sensor.BodyCenter();

        _motor.BeginAirborne();
        _motor.Launch(
            targetCenter.x - currentCenter.x,
            targetCenter.y - currentCenter.y,
            ClothCombatHandler.AttackMaxSpeedX,
            ClothCombatHandler.DiveGravityMultiplier,
            _sensor.GetCeilingClearance(),
            out float apexTime,
            out float totalFlightTime,
            out float launchGravityScale);

        _animator.Play("Jump");

        float elapsed = 0f;
        bool hasSlammed = false;
        bool hasPlayedAttackAnim = false;
        bool isDiving = false;

        while (elapsed < totalFlightTime + 2.5f) {
            elapsed += Time.deltaTime;

            if (!isDiving && elapsed >= apexTime) {
                _motor.SetGravityScale(launchGravityScale * ClothCombatHandler.DiveGravityMultiplier);
                isDiving = true;
            }

            Vector2 currentEnemyPosition = _combat.IsValidTarget(enemyTarget, gameObject) ? ClothSpatialSensor.EnemyCenter(enemyTarget) : targetCenter;
            float distanceToEnemy = Vector2.Distance(_sensor.BodyCenter(), currentEnemyPosition);
            bool isDescending = _motor.VelocityY < 0f;

            if (!hasPlayedAttackAnim && isDescending && (distanceToEnemy <= ClothCombatHandler.AttackAnimDistance || elapsed >= totalFlightTime - 0.25f)) {
                _animator.Play("Jump Attack");
                hasPlayedAttackAnim = true;
            }

            bool isLanded = elapsed > 0.25f && _motor.VelocityY <= 0.1f && _sensor.IsGrounded();

            if (!hasSlammed && (isLanded || elapsed >= totalFlightTime || (hasPlayedAttackAnim && distanceToEnemy <= ClothCombatHandler.AirHitDistance))) {
                if (!hasPlayedAttackAnim) {
                    _animator.Play("Jump Attack");
                    hasPlayedAttackAnim = true;
                }
                _combat.ExecuteSlamAreaDamage(gameObject, transform.position, ClothCombatHandler.DefaultSlamRadius, ClothCombatHandler.DefaultSlamDamageAmount);
                hasSlammed = true;
            }

            if (isLanded)
                break;
            yield return null;
        }

        if (!hasSlammed) {
            _combat.ExecuteSlamAreaDamage(gameObject, transform.position, ClothCombatHandler.DefaultSlamRadius, ClothCombatHandler.DefaultSlamDamageAmount);
        }

        _motor.EndAirborne();

        _animator.Play("Jump Attack Recover");
        var recoverClip = _animator.GetClipByName("Jump Attack Recover");
        float recoverDuration = recoverClip != null && recoverClip.fps > 0
            ? recoverClip.frames.Length / (float) recoverClip.fps
            : 0.4f;

        yield return new WaitForSeconds(recoverDuration);

        _animator.Play("Idle");

        _attackCooldown = 1.0f;
        _isBusy = false;
    }

    #endregion
}
