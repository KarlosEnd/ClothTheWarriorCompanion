using ClothTheWarriorCompanion.Config;

namespace ClothTheWarriorCompanion.Behaviors;

/// <summary>
/// Gestiona la física cinemática, colisionadores dinámicos, impulsos de salto y orientación visual de Cloth.
/// </summary>
internal sealed class ClothMotor {
    #region Tuning & Configuration

    private static ClothSettings Settings => ClothSettings.Current;

    public static float RestGravity => Settings.RestGravity;
    public static float MinJumpHeight => Settings.MinJumpHeight;
    public static float MaxJumpHeight => Settings.MaxJumpHeight;
    public static float JumpHeightBuffer => Settings.JumpHeightBuffer;
    public static float MinApexTime => Settings.MinApexTime;
    public static float MaxApexTime => Settings.MaxApexTime;
    public static float CeilingSafetyMargin => Settings.CeilingSafetyMargin;

    #endregion

    private readonly Transform _transform;
    private readonly Rigidbody2D _rigidbody;
    private readonly BoxCollider2D _normalCollider;
    private readonly CircleCollider2D _jumpCollider;

    public float FeetOffset { get; } = 1.2484f;
    public Rigidbody2D Rigidbody => _rigidbody;
    public BoxCollider2D NormalCollider => _normalCollider;
    public CircleCollider2D JumpCollider => _jumpCollider;
    public Vector2 Velocity => _rigidbody.velocity;
    public float VelocityY => _rigidbody.velocity.y;

    public ClothMotor(Transform transform, Rigidbody2D rigidbody, BoxCollider2D normalCollider) {
        _transform = transform;
        _rigidbody = rigidbody;
        _normalCollider = normalCollider;

        _jumpCollider = transform.gameObject.AddComponent<CircleCollider2D>();
        _jumpCollider.radius = _normalCollider.size.x * 0.5f;
        float normalBottomY = _normalCollider.offset.y - (_normalCollider.size.y * 0.5f);
        _jumpCollider.offset = new Vector2(_normalCollider.offset.x, normalBottomY + _jumpCollider.radius);
        _jumpCollider.enabled = false;

        // Distancia del centro del transform a los pies calculada desde el collider
        float calculatedOffset = ((_normalCollider.size.y * 0.5f) - _normalCollider.offset.y) * Mathf.Abs(transform.lossyScale.y);
        if (calculatedOffset > 0.1f) {
            FeetOffset = calculatedOffset;
        }

        _rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rigidbody.sleepMode = RigidbodySleepMode2D.NeverSleep;
        _rigidbody.freezeRotation = true;
        _rigidbody.drag = 0f;
    }

    public void ResetPhysics() {
        if (_jumpCollider != null) _jumpCollider.enabled = false;
        if (_normalCollider != null) {
            _normalCollider.enabled = true;
            _normalCollider.isTrigger = false;
        }
        if (_rigidbody != null) {
            _rigidbody.velocity = Vector2.zero;
            _rigidbody.gravityScale = RestGravity;
        }
    }

    public void PrepareForBurrowOrAppear() {
        _rigidbody.velocity = Vector2.zero;
        _rigidbody.gravityScale = 0f;
        _jumpCollider.enabled = false;
        _normalCollider.enabled = true;
        _normalCollider.isTrigger = true;
    }

    public void CompleteAppear() {
        _normalCollider.isTrigger = false;
        _rigidbody.gravityScale = RestGravity;
    }

    public void BeginAirborne() {
        _normalCollider.enabled = false;
        _jumpCollider.enabled = true;
    }

    public void EndAirborne() {
        _rigidbody.velocity = Vector2.zero;
        _rigidbody.gravityScale = RestGravity;
        _jumpCollider.enabled = false;
        _normalCollider.enabled = true;
    }

    public void SetGravityScale(float scale) {
        _rigidbody.gravityScale = scale;
    }

    /// <summary>
    /// Restablece la física normal (collider de pie, gravedad de reposo) y aplica un impulso de knockback.
    /// </summary>
    public void ApplyKnockback(Vector2 velocity) {
        ResetPhysics();
        _rigidbody.velocity = velocity;
    }

    public void SetVelocityX(float x) {
        Vector2 v = _rigidbody.velocity;
        v.x = x;
        _rigidbody.velocity = v;
    }

    public void TurnToFace(float horizontalDirection) {
        if (Mathf.Abs(horizontalDirection) < 0.01f) return;
        Vector3 scale = _transform.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(horizontalDirection);
        _transform.localScale = scale;
    }

    /// <summary>
    /// Lanza a Cloth en una parábola. Con jumpHeightOverride / apexTimeOverride > 0 se usan
    /// esos valores en lugar de los calculados (usado por el salto corto de ataque).
    /// forwardDirection != 0 impide que la velocidad horizontal apunte en sentido contrario a esa dirección.
    /// </summary>
    public void Launch(
        float distX,
        float distY,
        float maxVelocityX,
        float diveMultiplier,
        float ceilingClearance,
        out float apexTime,
        out float totalFlightTime,
        out float launchGravityScale,
        float jumpHeightOverride = -1f,
        float apexTimeOverride = -1f,
        float forwardDirection = 0f) {

        float jumpHeight;
        if (jumpHeightOverride > 0f) {
            // Hop bajo: solo lo necesario para llegar al objetivo
            jumpHeight = Mathf.Max(jumpHeightOverride, Mathf.Max(distY, 0f) + 0.25f);
            jumpHeight = Mathf.Min(jumpHeight, Mathf.Max(0.5f, ceilingClearance - CeilingSafetyMargin));
        } else {
            jumpHeight = Mathf.Clamp(Mathf.Max(distY, 0f) + JumpHeightBuffer, MinJumpHeight, MaxJumpHeight);
            jumpHeight = Mathf.Min(jumpHeight, Mathf.Max(1.5f, ceilingClearance - CeilingSafetyMargin));
        }

        apexTime = apexTimeOverride > 0f
            ? apexTimeOverride
            : Mathf.Clamp(0.30f + (jumpHeight * 0.055f), MinApexTime, MaxApexTime);

        float gravity = 2f * jumpHeight / (apexTime * apexTime);
        float velocityY = gravity * apexTime;

        float fallHeight = Mathf.Max(jumpHeight - distY, 0.01f);
        float fallTime = Mathf.Sqrt(2f * fallHeight / (gravity * Mathf.Max(1f, diveMultiplier)));
        totalFlightTime = apexTime + fallTime;

        float velocityX = Mathf.Clamp(distX / totalFlightTime, -maxVelocityX, maxVelocityX);

        // Solo hacia adelante: nunca invierte el sentido respecto a la orientación indicada
        if (Mathf.Abs(forwardDirection) > 0.01f) {
            float dir = Mathf.Sign(forwardDirection);
            velocityX = Mathf.Max(0f, velocityX * dir) * dir;
        }

        float baseGravity = Mathf.Max(1f, Mathf.Abs(Physics2D.gravity.y));
        launchGravityScale = gravity / baseGravity;
        _rigidbody.gravityScale = launchGravityScale;
        _rigidbody.velocity = new Vector2(velocityX, velocityY);
    }
}
