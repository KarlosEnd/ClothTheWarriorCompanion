using ClothTheWarriorCompanion.Config;

namespace ClothTheWarriorCompanion.Behaviors;

/// <summary>
/// Provee consultas espaciales, detección de terreno, resolución de puntos seguros
/// y chequeos de línea de visión para la compañera Cloth.
/// </summary>
internal sealed class ClothSpatialSensor {
    #region Tuning & Configuration

    private static ClothSettings Settings => ClothSettings.Current;

    public static float MaxVerticalReach => Settings.MaxVerticalReach;

    #endregion

    public const int TerrainMask = 1 << 8;
    private static readonly float[] SpotResolutionOffsets = { 0f, 1.5f, -1.5f, 3f, -3f, 5f, -5f };

    private readonly Transform _transform;
    private readonly BoxCollider2D _normalCollider;
    private readonly GameObject _hero;
    private readonly BoxCollider2D? _heroCollider;
    private readonly float _feetOffset;

    public ClothSpatialSensor(
        Transform transform,
        BoxCollider2D normalCollider,
        GameObject hero,
        BoxCollider2D? heroCollider,
        float feetOffset) {
        _transform = transform;
        _normalCollider = normalCollider;
        _hero = hero;
        _heroCollider = heroCollider;
        _feetOffset = feetOffset;
    }

    public Vector2 BodyCenter() =>
        (Vector2)_transform.position + Vector2.Scale(_normalCollider.offset, (Vector2)_transform.lossyScale);

    public Vector2 BodySize() {
        Vector3 lossy = _transform.lossyScale;
        return new Vector2(_normalCollider.size.x * Mathf.Abs(lossy.x), _normalCollider.size.y * Mathf.Abs(lossy.y));
    }

    public Vector2 Feet() {
        Vector2 center = BodyCenter();
        return new Vector2(center.x, center.y - (BodySize().y * 0.5f));
    }

    public Vector2 HeroCenter() =>
        _heroCollider != null ? (Vector2)_heroCollider.bounds.center : (Vector2)_hero.transform.position;

    private static readonly List<Collider2D> ColliderBuffer = new();

    /// <summary>
    /// Collider "de cuerpo" del objetivo: prefiere uno sólido en el propio objeto, luego un trigger en el propio objeto,
    /// y por último un sólido en los hijos. Ignora triggers hijos (rangos de detección, sensores...).
    /// </summary>
    public static Collider2D? GetBodyCollider(GameObject target) {
        Collider2D? solid = null;
        Collider2D? trigger = null;

        target.GetComponents(ColliderBuffer);
        for (int i = 0; i < ColliderBuffer.Count; i++) {
            var c = ColliderBuffer[i];
            if (c == null || !c.enabled) continue;
            if (!c.isTrigger) { solid = c; break; }
            trigger ??= c;
        }

        if (solid == null && trigger == null) {
            target.GetComponentsInChildren(false, ColliderBuffer);
            for (int i = 0; i < ColliderBuffer.Count; i++) {
                var c = ColliderBuffer[i];
                if (c == null || !c.enabled || c.isTrigger) continue;
                solid = c;
                break;
            }
        }

        ColliderBuffer.Clear();
        return solid ?? trigger;
    }

    public static Vector2 EnemyCenter(GameObject target) {
        var col = GetBodyCollider(target);
        return col != null ? (Vector2)col.bounds.center : (Vector2)target.transform.position;
    }

    /// <summary>
    /// Separación (por eje) entre un punto y el collider del objetivo. 0 en un eje = el punto queda dentro del rango del collider en ese eje.
    /// </summary>
    public static Vector2 GetGapToTarget(GameObject target, Vector2 from) {
        var col = GetBodyCollider(target);
        if (col == null) {
            Vector2 p = target.transform.position;
            return new Vector2(Mathf.Abs(p.x - from.x), Mathf.Abs(p.y - from.y));
        }
        UnityEngine.Bounds b = col.bounds;
        return new Vector2(
            Mathf.Max(0f, Mathf.Abs(from.x - b.center.x) - b.extents.x),
            Mathf.Max(0f, Mathf.Abs(from.y - b.center.y) - b.extents.y));
    }

    public Vector3 FeetToPosition(Vector2 feetGroundPosition) {
        float offsetX = _normalCollider.offset.x * _transform.lossyScale.x;
        return new Vector3(
            feetGroundPosition.x - offsetX,
            feetGroundPosition.y + _feetOffset + 0.05f,
            _hero.transform.position.z + 0.011f
        );
    }

    public bool IsBlockedAt(Vector3 position) {
        Vector2 center = (Vector2)position + Vector2.Scale(_normalCollider.offset, (Vector2)_transform.lossyScale);
        return Physics2D.OverlapBox(center, BodySize() * 0.85f, 0f, TerrainMask) != null;
    }

    public bool IsEmbedded() =>
        Physics2D.OverlapBox(BodyCenter(), BodySize() * 0.7f, 0f, TerrainMask) != null;

    public Vector3 ResolveSafeSpot(Vector2 feetGroundPosition) {
        foreach (float offsetX in SpotResolutionOffsets) {
            Vector2 evaluatedFeet = feetGroundPosition;
            if (offsetX != 0f && !ClothCompanionManager.TryFindGround(new Vector2(feetGroundPosition.x + offsetX, feetGroundPosition.y + 1f), out evaluatedFeet, 4f)) {
                continue;
            }

            Vector3 candidatePosition = FeetToPosition(evaluatedFeet);
            if (!IsBlockedAt(candidatePosition)) {
                return candidatePosition;
            }
        }
        return FeetToPosition(feetGroundPosition);
    }

    public bool IsGrounded() {
        Vector2 feet = Feet();
        float halfWidth = BodySize().x * 0.4f;
        Vector2 origin = feet + (Vector2.up * 0.15f);

        return Physics2D.Raycast(origin, Vector2.down, 0.4f, TerrainMask).collider != null
            || Physics2D.Raycast(origin + (Vector2.left * halfWidth), Vector2.down, 0.4f, TerrainMask).collider != null
            || Physics2D.Raycast(origin + (Vector2.right * halfWidth), Vector2.down, 0.4f, TerrainMask).collider != null;
    }

    public float GetCeilingClearance() => GetCeilingClearanceAt(_transform.position);

    /// <summary>
    /// Espacio libre sobre la cabeza de Cloth si su transform estuviera en la posición indicada.
    /// </summary>
    public float GetCeilingClearanceAt(Vector2 position, float maxDistance = 12f) {
        Vector2 center = position + Vector2.Scale(_normalCollider.offset, (Vector2)_transform.lossyScale);
        Vector2 top = center + (Vector2.up * (BodySize().y * 0.5f));
        RaycastHit2D hit = Physics2D.Raycast(top, Vector2.up, maxDistance, TerrainMask);
        return hit.collider != null ? hit.distance : maxDistance;
    }

    public float GetMaxReach() => Mathf.Min(MaxVerticalReach, GetCeilingClearance() - 0.5f);

    public static bool HasLineOfSight(Vector2 origin, Vector2 target) {
        Vector2 delta = target - origin;
        float distance = delta.magnitude;
        if (distance < 0.5f) return true;

        Vector2 adjustedTarget = target - (delta / distance * Mathf.Min(0.6f, distance * 0.5f));
        return Physics2D.Linecast(origin, adjustedTarget, TerrainMask).collider == null;
    }
}
