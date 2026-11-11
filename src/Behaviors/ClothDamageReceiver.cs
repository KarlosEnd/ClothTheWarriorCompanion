using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ClothTheWarriorCompanion.Config;

namespace ClothTheWarriorCompanion.Behaviors;

/// <summary>
/// Subsistema encargado de la recepción de daño, invulnerabilidad, cálculo de knockback ágil y
/// reacciones defensivas avanzadas (aturdimiento, contraataque ligero/pesado, escape y derrota).
/// </summary>
internal sealed class ClothDamageReceiver {
    private static ClothSettings Settings => ClothSettings.Current;

    private readonly MonoBehaviour _host;
    private readonly tk2dSpriteAnimator _animator;
    private readonly ClothMotor _motor;
    private readonly ClothSpatialSensor _sensor;
    private readonly ClothCombatHandler _combat;
    private readonly ClothJumpPlanner _planner;
    private readonly ClothTrajectoryPredictor _predictor;
    private readonly Func<ClothCombatBehavior> _getCombatBehavior;
    private readonly Action<Vector2> _appearAt;

    private int _damageTaken = 0;
    private float _invulnTimer = 0f;
    private float _escapeCooldown = 0f;
    private float _counterCooldown = 0f;
    private float _slashScanTimer = 0f;
    private bool _isBusy = false;
    private bool _isDowned = false;

    private readonly List<float> _recentHits = new();
    private readonly Collider2D[] _damageBuffer = new Collider2D[64];
    private readonly List<Collider2D> _slashBuffer = new();

    public bool IsBusy => _isBusy;
    public bool IsDowned => _isDowned;
    public int DamageTaken => _damageTaken;
    public float InvulnTimer => _invulnTimer;

    public ClothDamageReceiver(
        MonoBehaviour host,
        tk2dSpriteAnimator animator,
        ClothMotor motor,
        ClothSpatialSensor sensor,
        ClothCombatHandler combat,
        ClothJumpPlanner planner,
        ClothTrajectoryPredictor predictor,
        Func<ClothCombatBehavior> getCombatBehavior,
        Action<Vector2> appearAt) {

        _host = host;
        _animator = animator;
        _motor = motor;
        _sensor = sensor;
        _combat = combat;
        _planner = planner;
        _predictor = predictor;
        _getCombatBehavior = getCombatBehavior;
        _appearAt = appearAt;
    }

    public void ResetState() {
        _isBusy = false;
        _isDowned = false;
        _motor.ResetPhysics();
    }

    public void FullReset() {
        ResetState();
        _damageTaken = 0;
        _recentHits.Clear();
        _invulnTimer = 0f;
        _escapeCooldown = 0f;
        _counterCooldown = 0f;
    }

    public void UpdateTimers(bool isTargetable) {
        if (_invulnTimer > 0f) _invulnTimer -= Time.deltaTime;
        if (_escapeCooldown > 0f) _escapeCooldown -= Time.deltaTime;
        if (_counterCooldown > 0f) _counterCooldown -= Time.deltaTime;

        CheckIncomingDamage(isTargetable);
        CheckKnightSlash(isTargetable);
    }

    private void CheckIncomingDamage(bool isTargetable) {
        if (!Settings.CanTakeDamage || _invulnTimer > 0f || !isTargetable)
            return;

        Vector2 center = _sensor.BodyCenter();
        Vector2 size = _sensor.BodySize() * 0.9f;
        int count = Physics2D.OverlapBoxNonAlloc(center, size, 0f, _damageBuffer, ~ClothSpatialSensor.TerrainMask);

        for (int i = 0; i < count; i++) {
            var col = _damageBuffer[i];
            if (col == null || col.transform.IsChildOf(_host.transform))
                continue;

            var damageHero = col.GetComponent<DamageHero>();
            if (damageHero == null || !damageHero.isActiveAndEnabled || damageHero.damageDealt <= 0)
                continue;

            if (damageHero.hazardType > 1)
                continue;

            var owner = col.GetComponentInParent<HealthManager>();
            Vector2 sourcePosition = owner != null ? owner.transform.position : col.transform.position;

            ReceiveHit(sourcePosition, damageHero.damageDealt, owner != null ? owner.gameObject : null);
            return;
        }
    }

    private void CheckKnightSlash(bool isTargetable) {
        if (!Settings.CanTakeDamage || !Settings.KnightCanHurtCloth || Settings.KnightHitDamage <= 0)
            return;
        if (_invulnTimer > 0f || !isTargetable || HeroController.instance == null)
            return;

        _slashScanTimer -= Time.deltaTime;
        if (_slashScanTimer > 0f)
            return;
        _slashScanTimer = 0.05f;

        Collider2D? bodyCollider = _motor.NormalCollider.enabled
            ? _motor.NormalCollider
            : (_motor.JumpCollider.enabled ? _motor.JumpCollider : null);
        if (bodyCollider == null)
            return;

        bool hit = false;
        foreach (var attack in UObject.FindObjectsOfType<DamageEnemies>()) {
            if (attack == null || !attack.isActiveAndEnabled || attack.transform.IsChildOf(_host.transform))
                continue;

            attack.GetComponents(_slashBuffer);
            for (int i = 0; i < _slashBuffer.Count; i++) {
                var attackCollider = _slashBuffer[i];
                if (attackCollider == null || !attackCollider.enabled)
                    continue;

                if (attackCollider.Distance(bodyCollider).isOverlapped) {
                    hit = true;
                    break;
                }
            }
            _slashBuffer.Clear();

            if (hit)
                break;
        }
        _slashBuffer.Clear();

        if (hit) {
            ReceiveKnightHit();
        }
    }

    public void ReceiveKnightHit() {
        if (!Settings.CanTakeDamage || !Settings.KnightCanHurtCloth || Settings.KnightHitDamage <= 0)
            return;
        if (_invulnTimer > 0f || _isDowned || HeroController.instance == null)
            return;

        ReceiveHit(HeroController.instance.transform.position, Settings.KnightHitDamage, HeroController.instance.gameObject);
    }

    /// <summary>
    /// Recibe un impacto nativo de Hollow Knight (HitInstance) proveniente del Caballero (clavo, artes, hechizos).
    /// </summary>
    public void ReceiveHitInstance(HitInstance hitInstance) {
        if (!Settings.CanTakeDamage || !Settings.KnightCanHurtCloth || hitInstance.DamageDealt <= 0)
            return;
        if (_invulnTimer > 0f || _isDowned)
            return;

        Vector2 sourcePos = hitInstance.Source != null
            ? (Vector2)hitInstance.Source.transform.position
            : (HeroController.instance != null ? (Vector2)HeroController.instance.transform.position : _sensor.BodyCenter());

        int damage = Mathf.Max(1, hitInstance.DamageDealt);
        ReceiveHit(sourcePos, damage, hitInstance.Source, hitInstance.Direction);
    }

    private void ReceiveHit(Vector2 sourcePosition, int damage, GameObject? attacker = null, float hitAngle = -1f) {
        _damageTaken += Mathf.Max(1, damage);
        _invulnTimer = Mathf.Max(0f, Settings.HurtInvulnerability);
        bool defeated = Settings.MaxHealth > 0 && _damageTaken >= Settings.MaxHealth;

        float dx = _sensor.BodyCenter().x - sourcePosition.x;
        float direction = Mathf.Abs(dx) > 0.05f ? Mathf.Sign(dx) : -ClothCombatHandler.GetFacing(_host.transform);

        // Si se especificó ángulo de ataque de Hollow Knight (0 = derecha, 180 = izquierda)
        if (hitAngle >= 0f) {
            if (hitAngle > 45f && hitAngle < 135f) {
                // Golpe hacia arriba
                direction = dx >= 0f ? 1f : -1f;
            } else if (hitAngle > 225f && hitAngle < 315f) {
                // Golpe hacia abajo
                direction = dx >= 0f ? 1f : -1f;
            } else if (hitAngle >= 135f && hitAngle <= 225f) {
                direction = -1f; // Empuje a la izquierda
            } else {
                direction = 1f; // Empuje a la derecha
            }
        }

        if (attacker != null) {
            _predictor.Track(attacker, ClothSpatialSensor.EnemyCenter(attacker));
        }

        var audio = _host.GetComponent<ClothAudioController>();
        if (!defeated) {
            audio?.PlayHurt();
        }

        ClothTheWarriorCompanionMod.Instance?.LogDebug($"Cloth recibió {damage} de daño ({_damageTaken}/{Settings.MaxHealth}).");

        float now = Time.time;
        float window = Mathf.Max(Settings.EscapeHitWindow, Settings.CounterHitWindow);
        _recentHits.Add(now);
        _recentHits.RemoveAll(t => now - t > window);

        float healthPercent = Settings.MaxHealth > 0
            ? Mathf.Max(0f, (Settings.MaxHealth - _damageTaken) * 100f / Settings.MaxHealth)
            : 100f;
        bool unlimitedHealth = Settings.MaxHealth <= 0;

        bool escape = !defeated
            && Settings.EscapeEnabled
            && _escapeCooldown <= 0f
            && CountRecentHits(now, Settings.EscapeHitWindow) >= Mathf.Max(1, Settings.EscapeHitCount)
            && (unlimitedHealth || healthPercent <= Settings.EscapeHealthPercent);

        bool heavyCounter = !defeated
            && !escape
            && Settings.CounterEnabled
            && _counterCooldown <= 0f
            && CountRecentHits(now, Settings.CounterHitWindow) >= Mathf.Max(1, Settings.CounterHitCount)
            && (unlimitedHealth || healthPercent <= Settings.CounterHealthPercent)
            && attacker != null
            && IsCounterTarget(attacker);

        bool lightCounter = !defeated
            && !escape
            && !heavyCounter
            && Settings.CounterEnabled
            && attacker != null
            && IsCounterTarget(attacker);

        // Cancelar acciones previas e iniciar respuesta al golpe
        var follow = _host.GetComponent<ClothFollowBehavior>();
        follow?.ResetState();

        // Calcular vector 2D de impulso según la dirección real del ataque
        Vector2 knockbackImpulse = CalculateKnockbackImpulse(direction, sourcePosition, hitAngle);

        if (defeated) {
            _recentHits.Clear();
            _host.StartCoroutine(DefeatedCoroutine(direction, knockbackImpulse));
        } else if (escape) {
            _recentHits.Clear();
            _escapeCooldown = Mathf.Max(0f, Settings.EscapeCooldown);
            _host.StartCoroutine(EscapeCoroutine(direction, knockbackImpulse));
        } else if (heavyCounter) {
            _recentHits.Clear();
            _counterCooldown = Mathf.Max(0f, Settings.CounterCooldown);
            _host.StartCoroutine(CounterCoroutine(direction, attacker!, knockbackImpulse));
        } else if (lightCounter) {
            _host.StartCoroutine(LightCounterCoroutine(direction, attacker!, knockbackImpulse));
        } else {
            _host.StartCoroutine(HurtCoroutine(direction, knockbackImpulse));
        }
    }

    /// <summary>
    /// Calcula el vector de velocidad del knockback según el ángulo de ataque y el origen del golpe.
    /// Evita elevar siempre hacia arriba si el golpe vino desde arriba (downward slam o pogo).
    /// </summary>
    private Vector2 CalculateKnockbackImpulse(float horizontalSign, Vector2 sourcePosition, float hitAngle) {
        float speedX = Settings.KnockbackSpeedX;
        float speedY = Settings.KnockbackSpeedY;

        if (!Settings.KnockbackDirectional) {
            return new Vector2(horizontalSign * speedX, speedY);
        }

        // 1. Si viene ángulo nativo de Hollow Knight
        if (hitAngle >= 0f) {
            if (hitAngle > 225f && hitAngle < 315f) {
                // Golpe hacia abajo (pogo del Caballero o slam desde el aire): aplasta contra el suelo
                return new Vector2(horizontalSign * speedX * 0.6f, -3.5f);
            }
            if (hitAngle > 45f && hitAngle < 135f) {
                // Golpe hacia arriba (uppercut o chirrido): elevación potente
                return new Vector2(horizontalSign * speedX * 0.7f, speedY * 1.35f);
            }
            // Golpe lateral estándar: desplazamiento horizontal fluido con elevación leve y natural
            return new Vector2(horizontalSign * speedX, speedY * 0.35f);
        }

        // 2. Si se calcula respecto a la posición del agresor
        Vector2 delta = _sensor.BodyCenter() - sourcePosition;
        if (delta.y < -0.4f) {
            // El agresor está por encima de Cloth: impacto hacia abajo
            return new Vector2(horizontalSign * speedX * 0.65f, -3.0f);
        }
        if (delta.y > 0.45f) {
            // El agresor está por debajo: elevación
            return new Vector2(horizontalSign * speedX * 0.75f, speedY * 1.25f);
        }

        // Horizontal
        return new Vector2(horizontalSign * speedX, speedY * 0.35f);
    }

    private int CountRecentHits(float now, float window) {
        int count = 0;
        for (int i = 0; i < _recentHits.Count; i++) {
            if (now - _recentHits[i] <= window)
                count++;
        }
        return count;
    }

    private bool IsCounterTarget(GameObject attacker) {
        if (ClothCombatHandler.IsPlayer(attacker) || !_combat.IsValidTarget(attacker, _host.gameObject))
            return false;

        return Vector2.Distance(_sensor.BodyCenter(), ClothSpatialSensor.EnemyCenter(attacker)) <= Settings.CounterMaxDistance;
    }

    private void PlayHurtAnimation() {
        float fps = Settings.HurtAnimFps;
        if (fps <= 0f) return;

        var clip = _animator.GetClipByName("Jump Attack Antic");
        if (clip != null) {
            _animator.Play(clip, 0f, fps);
        } else {
            _animator.Play("Jump Attack Antic");
        }
    }

    /// <summary>
    /// Impulso de knockback cinemático refinado:
    /// Aplica vector 2D según la dirección del golpe, desaceleración natural, gravedad adecuada
    /// y recuperación instantánea en aterrizaje (configurable en QuickMenu).
    /// </summary>
    public IEnumerator KnockbackRoutine(Vector2 knockbackImpulse) {
        float freeze = Mathf.Max(0f, Settings.HitFreezeDuration);
        if (freeze > 0f) {
            _motor.Freeze();
            PlayHurtAnimation();
            yield return new WaitForSeconds(freeze);
        }

        // Si el golpe viene desde arriba (y < 0), la gravedad mayor asienta inmediatamente a Cloth en el suelo
        float grav = knockbackImpulse.y < 0f ? Settings.KnockbackGravityScale * 1.5f : Settings.KnockbackGravityScale;
        _motor.ApplyKnockback(knockbackImpulse, grav);

        PlayHurtAnimation();

        float stunDuration = Mathf.Max(0.04f, Settings.HurtStunDuration);
        float animDuration = Mathf.Max(0.04f, Settings.HurtAnimDuration);
        float decelRate = Mathf.Max(5f, Settings.KnockbackDeceleration);
        bool recoverOnLand = Settings.KnockbackRecoverOnLand;

        float elapsed = 0f;
        bool animFinished = false;

        // Fase de aturdimiento y desaceleración
        while (elapsed < stunDuration) {
            elapsed += Time.deltaTime;
            _motor.DecelerateVelocityX(decelRate, Time.deltaTime);

            if (!animFinished && elapsed >= animDuration) {
                animFinished = true;
                if (_motor.VelocityY < -0.1f) {
                    _animator.Play("Jump"); // Caída natural en el aire
                }
            }

            yield return null;
        }

        // Recuperación tras caída: si toca el suelo o termina el tiempo de aturdimiento máximo, recupera el control
        float maxAirWait = Mathf.Max(0.1f, Mathf.Min(0.45f, stunDuration * 1.5f));
        float airElapsed = 0f;

        while (airElapsed < maxAirWait) {
            airElapsed += Time.deltaTime;
            _motor.DecelerateVelocityX(decelRate * 1.5f, Time.deltaTime);

            if (recoverOnLand && _motor.VelocityY <= 0.1f && _sensor.IsGrounded()) {
                break;
            }

            yield return null;
        }
    }

    public IEnumerator KnockbackRoutine(float direction) =>
        KnockbackRoutine(new Vector2(direction * Settings.KnockbackSpeedX, Settings.KnockbackSpeedY * 0.35f));

    private IEnumerator HurtCoroutine(float direction, Vector2 impulse) {
        _isBusy = true;
        _motor.TurnToFace(-direction);

        yield return KnockbackRoutine(impulse);

        _animator.Play("Idle");
        var combatBehavior = _getCombatBehavior();
        if (combatBehavior != null) {
            combatBehavior.SetAttackCooldown(0.2f);
        }
        _isBusy = false;
    }

    private IEnumerator LightCounterCoroutine(float knockbackDirection, GameObject enemy, Vector2 impulse) {
        _isBusy = true;
        _motor.TurnToFace(-knockbackDirection);

        yield return KnockbackRoutine(impulse);

        if (!_combat.IsValidTarget(enemy, _host.gameObject)) {
            _animator.Play("Idle");
            _isBusy = false;
            yield break;
        }

        _invulnTimer = Mathf.Max(_invulnTimer, Settings.HurtInvulnerability + 0.4f);

        float facing = -knockbackDirection;
        Vector2 myFeet = _sensor.Feet();
        if (!_planner.TryFindRetreatLanding(myFeet, knockbackDirection, Settings.UseJumpPlanner, out Vector2 landingFeet)) {
            landingFeet = myFeet + new Vector2(knockbackDirection * 2.8f, 0f);
        }

        var combatBehavior = _getCombatBehavior();
        if (combatBehavior != null) {
            _host.GetComponent<ClothAudioController>()?.PlayJump();
            yield return combatBehavior.ExecuteHopRoutine(
                facing, landingFeet,
                ClothCombatHandler.RetreatJumpHeight,
                ClothCombatHandler.RetreatApexTime,
                ClothCombatHandler.RetreatSpeedX,
                ClothCombatHandler.RetreatDiveGravityMultiplier,
                ClothCombatHandler.RetreatAnticDuration,
                ClothCombatHandler.RetreatLandCheckDelay,
                ClothCombatHandler.RetreatRecoverDurationScale,
                playJumpAnimation: false,
                endWithIdle: false
            );

            if (_combat.IsValidTarget(enemy, _host.gameObject)) {
                yield return combatBehavior.AttackCoroutine(enemy, -1f, -1f, forceClose: true);
                yield break;
            }
        }

        _animator.Play("Idle");
        _isBusy = false;
    }

    private IEnumerator CounterCoroutine(float knockbackDirection, GameObject enemy, Vector2 impulse) {
        _isBusy = true;
        _motor.TurnToFace(-knockbackDirection);

        yield return KnockbackRoutine(impulse);

        if (!_combat.IsValidTarget(enemy, _host.gameObject)) {
            _animator.Play("Idle");
            _isBusy = false;
            yield break;
        }

        _host.GetComponent<ClothAudioController>()?.PlayBattleCry();

        Vector2 lastTarget = ClothSpatialSensor.EnemyCenter(enemy);
        float facing = lastTarget.x >= _host.transform.position.x ? 1f : -1f;
        float back = -facing;
        _motor.TurnToFace(facing);
        _invulnTimer = Mathf.Max(_invulnTimer, Settings.CounterInvulnerability);

        _animator.Play("Jump Attack Antic");
        yield return new WaitForSeconds(Mathf.Max(0f, Settings.CounterAnticDuration));

        float dive = Mathf.Max(1f, Settings.CounterDiveMultiplier);
        var sol = ClothMotor.ComputeLaunch(
            0f, 0f, 0f,
            dive,
            _sensor.GetCeilingClearanceAt(_host.transform.position, Settings.CounterJumpHeight + 2f),
            Settings.CounterJumpHeight,
            Settings.CounterApexTime,
            0f);

        _motor.BeginAirborne();
        _motor.ApplySolution(sol, back * Settings.CounterBackSpeedX);
        _animator.Play("Jump");

        float diveGravity = sol.EffectiveGravity * dive;
        float maxSpeedX = Mathf.Max(1f, Settings.CounterMaxSpeedX);
        bool isDiving = false;
        bool hasSlammed = false;
        bool hasPlayedAttackAnim = false;
        float elapsed = 0f;
        float slamElapsed = 0f;
        int fallPhase = 0;
        float fallPhaseStart = 0f;
        float jumpPhaseDuration = 0f;

        while (elapsed < sol.ApexTime + 5f) {
            elapsed += Time.deltaTime;
            _motor.TurnToFace(facing);

            Vector2 attackCenter = ClothCombatHandler.GetAttackBoxCenter(_host.transform.position, facing);

            if (!isDiving && elapsed >= sol.ApexTime) {
                isDiving = true;
                _motor.SetGravityScale(sol.GravityScale * dive);
            }

            if (_combat.IsValidTarget(enemy, _host.gameObject)) {
                float drop = Mathf.Max(0f, attackCenter.y - ClothSpatialSensor.EnemyCenter(enemy).y);
                float estimatedTimeToTarget = 0.25f;
                if (isDiving && diveGravity > 0f) {
                    float vy = _motor.VelocityY;
                    estimatedTimeToTarget = (vy + Mathf.Sqrt((vy * vy) + (2f * diveGravity * drop))) / diveGravity;
                }
                lastTarget = _predictor.PredictPosition(enemy, Mathf.Max(0.05f, estimatedTimeToTarget));
            }

            if (isDiving && !hasSlammed) {
                float drop = Mathf.Max(0f, attackCenter.y - lastTarget.y);
                float vy = _motor.VelocityY;
                float timeToTarget = (vy + Mathf.Sqrt((vy * vy) + (2f * diveGravity * drop))) / diveGravity;
                if (timeToTarget > 0.05f) {
                    float vx = Mathf.Clamp((lastTarget.x - attackCenter.x) / timeToTarget, -maxSpeedX, maxSpeedX);
                    _motor.SetVelocityX(vx);
                }
            }

            float distance = Vector2.Distance(attackCenter, lastTarget);
            bool isDescending = _motor.VelocityY < 0f;

            if (!hasPlayedAttackAnim && isDiving && isDescending && distance <= ClothCombatHandler.AttackAnimDistance) {
                _animator.Play("Jump Attack");
                hasPlayedAttackAnim = true;
            }

            bool isLanded = elapsed > ClothCombatHandler.AttackLandCheckDelay && _motor.VelocityY <= 0.1f && _sensor.IsGrounded();

            if (!hasSlammed && (isLanded || (isDiving && isDescending && distance <= ClothCombatHandler.AirHitDistance))) {
                if (!hasPlayedAttackAnim) {
                    _animator.Play("Jump Attack");
                    hasPlayedAttackAnim = true;
                }
                DoSlam(facing, Settings.CounterSlamDamageAmount);
                hasSlammed = true;
                slamElapsed = elapsed;
            }

            if (hasSlammed && elapsed - slamElapsed <= Settings.AttackActiveDuration) {
                DoSlam(facing, Settings.CounterSlamDamageAmount);
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
            DoSlam(facing, Settings.CounterSlamDamageAmount);
        }

        _motor.EndAirborne();

        int recoverStartFrame = fallPhase > 0 ? Mathf.Max(0, ClothCombatHandler.PostAttackFallRecoverStartFrame - 1) : 0;
        yield return PlayRecover(ClothCombatHandler.RecoverDurationScale, recoverStartFrame);

        _animator.Play("Idle");
        _isBusy = false;
    }

    private IEnumerator EscapeCoroutine(float direction, Vector2 impulse) {
        _isBusy = true;
        _motor.TurnToFace(-direction);

        yield return KnockbackRoutine(impulse);

        Vector2 myFeet = _sensor.Feet();
        if (_planner.TryPlanEscape(myFeet, direction, out Vector2 landing, out float escapeHeight, out float escapeApex)) {
            _invulnTimer = Mathf.Max(_invulnTimer, Settings.EscapeInvulnerability);

            float dir = landing.x - myFeet.x;
            var combatBehavior = _getCombatBehavior();
            if (combatBehavior != null) {
                _host.GetComponent<ClothAudioController>()?.PlayJump();
                yield return combatBehavior.ExecuteHopRoutine(
                    Mathf.Abs(dir) > 0.01f ? Mathf.Sign(dir) : -direction,
                    landing,
                    escapeHeight,
                    escapeApex,
                    Settings.EscapeSpeedX,
                    Settings.EscapeDiveMultiplier,
                    Settings.EscapeAnticDuration,
                    0.25f,
                    Settings.JumpRecoverDurationScale,
                    playJumpAnimation: true
                );
            }
            _isBusy = false;
        } else {
            var follow = _host.GetComponent<ClothFollowBehavior>();
            if (follow != null) {
                follow.ForceBurrow(ClothCompanionManager.GetKnightGroundPosition());
            }
        }
    }

    private IEnumerator DefeatedCoroutine(float direction, Vector2 impulse) {
        _isBusy = true;
        _isDowned = true;
        _motor.TurnToFace(-direction);

        yield return KnockbackRoutine(impulse);

        _host.GetComponent<ClothAudioController>()?.PlayDeath();
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

        var mr = _host.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;

        float wait = Mathf.Max(0f, Settings.DefeatedReturnDelay);
        float elapsed = 0f;
        while (elapsed < wait) {
            elapsed += Time.deltaTime;
            yield return null;
        }

        _damageTaken = 0;
        _isDowned = false;
        _appearAt(ClothCompanionManager.GetKnightGroundPosition());
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

