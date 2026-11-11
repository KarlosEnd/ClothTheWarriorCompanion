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

    /// <summary>Radio horizontal del cuerpo sólido en unidades de mundo.</summary>
    public float BodyRadius => _normalCollider != null
        ? (_normalCollider.size.x * 0.5f * Mathf.Abs(_transform.lossyScale.x))
        : 1.2f;

    /// <summary>Radio del collider de salto en unidades de mundo.</summary>
    public float JumpCircleRadius => _jumpCollider.radius * Mathf.Abs(_transform.lossyScale.x);

    /// <summary>Desplazamiento vertical del centro del collider de salto respecto al transform, en unidades de mundo.</summary>
    public float JumpCircleOffsetY => _jumpCollider.offset.y * Mathf.Abs(_transform.lossyScale.y);
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
    /// Restablece la física normal y aplica un impulso de knockback con escala de gravedad personalizada.
    /// </summary>
    public void ApplyKnockback(Vector2 velocity, float gravityScale = -1f) {
        ResetPhysics();
        _rigidbody.velocity = velocity;
        _rigidbody.gravityScale = gravityScale > 0f ? gravityScale : Settings.KnockbackGravityScale;
    }

    public void SetVelocityX(float x) {
        Vector2 v = _rigidbody.velocity;
        v.x = x;
        _rigidbody.velocity = v;
    }

    /// <summary>
    /// Desacelera suavemente la velocidad horizontal hacia cero según la tasa especificada por segundo.
    /// </summary>
    public void DecelerateVelocityX(float rate, float dt) {
        Vector2 v = _rigidbody.velocity;
        v.x = Mathf.MoveTowards(v.x, 0f, rate * dt);
        _rigidbody.velocity = v;
    }

    /// <summary>
    /// Congela a Cloth donde está (sin velocidad ni gravedad). El siguiente ResetPhysics/ApplyKnockback la libera.
    /// </summary>
    public void Freeze() {
        _rigidbody.velocity = Vector2.zero;
        _rigidbody.gravityScale = 0f;
    }

    /// <summary>
    /// Aplica una solución de salto ya calculada, con una velocidad horizontal propia.
    /// </summary>
    public void ApplySolution(in LaunchSolution sol, float velocityX) {
        _rigidbody.gravityScale = sol.GravityScale;
        _rigidbody.velocity = new Vector2(velocityX, sol.VelocityY);
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

        var sol = ComputeLaunch(distX, distY, maxVelocityX, diveMultiplier, ceilingClearance, jumpHeightOverride, apexTimeOverride, forwardDirection);

        apexTime = sol.ApexTime;
        totalFlightTime = sol.TotalFlightTime;
        launchGravityScale = sol.GravityScale;

        _rigidbody.gravityScale = sol.GravityScale;
        _rigidbody.velocity = new Vector2(sol.VelocityX, sol.VelocityY);
    }

    /// <summary>
    /// Calcula la parábola de un salto sin aplicarla. Es la misma matemática que usa Launch,
    /// por lo que el planificador puede simular exactamente lo que se ejecutaría.
    /// </summary>
    public static LaunchSolution ComputeLaunch(
        float distX,
        float distY,
        float maxVelocityX,
        float diveMultiplier,
        float ceilingClearance,
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

        float apexTime = apexTimeOverride > 0f
            ? apexTimeOverride
            : Mathf.Clamp(0.30f + (jumpHeight * 0.055f), MinApexTime, MaxApexTime);

        float gravity = 2f * jumpHeight / (apexTime * apexTime);
        float velocityY = gravity * apexTime;

        float fallHeight = Mathf.Max(jumpHeight - distY, 0.01f);
        float fallTime = Mathf.Sqrt(2f * fallHeight / (gravity * Mathf.Max(1f, diveMultiplier)));
        float totalFlightTime = apexTime + fallTime;

        float velocityX = Mathf.Clamp(distX / totalFlightTime, -maxVelocityX, maxVelocityX);

        // Solo hacia adelante: nunca invierte el sentido respecto a la orientación indicada
        if (Mathf.Abs(forwardDirection) > 0.01f) {
            float dir = Mathf.Sign(forwardDirection);
            velocityX = Mathf.Max(0f, velocityX * dir) * dir;
        }

        float physicsGravity = Mathf.Abs(Physics2D.gravity.y);
        float gravityScale = gravity / Mathf.Max(1f, physicsGravity);

        return new LaunchSolution {
            VelocityX = velocityX,
            VelocityY = velocityY,
            Gravity = gravity,
            GravityScale = gravityScale,
            EffectiveGravity = gravityScale * physicsGravity,
            ApexTime = apexTime,
            TotalFlightTime = totalFlightTime,
            JumpHeight = jumpHeight,
            DiveMultiplier = diveMultiplier
        };
    }
}

/// <summary>
/// Resultado de calcular un salto: velocidades iniciales, gravedad y tiempos.
/// </summary>
internal struct LaunchSolution {
    public float VelocityX;
    public float VelocityY;
    public float Gravity;
    public float GravityScale;
    public float EffectiveGravity;
    public float ApexTime;
    public float TotalFlightTime;
    public float JumpHeight;
    public float DiveMultiplier;
}
