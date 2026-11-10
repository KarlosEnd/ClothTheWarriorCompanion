using System;
using System.Collections.Generic;

using ClothTheWarriorCompanion.Behaviors; // Para acceder a los radios de combate

using UnityEngine;

namespace ClothTheWarriorCompanion.DebugTools;

/// <summary>
/// Componente modular para visualizar hitboxes, triggers y radios de combate.
/// Se actualiza dinámicamente en tiempo real si los valores cambian.
/// </summary>
internal class ClothVisualDebugger : MonoBehaviour {
    public static bool IsActive = false;

    private readonly List<GameObject> _visualizers = new();
    private readonly List<Action> _updateActions = new(); // Lista de tareas para actualizar cada frame
    private Material? _lineMaterial;
    private bool _currentlyShowing = false;
    private const int CircleSegments = 36;

    private void Start() {
        _lineMaterial = new Material(Shader.Find("Sprites/Default"));
    }

    private void Update() {
        if (IsActive) {
            if (!_currentlyShowing) {
                CreateVisualizers();
                _currentlyShowing = true;
            }

            // Recalcular posiciones en tiempo real cada frame
            foreach (var updateAction in _updateActions) {
                updateAction?.Invoke();
            }
        } else if (!IsActive && _currentlyShowing) {
            ClearVisualizers();
            _currentlyShowing = false;
        }
    }

    private void CreateVisualizers() {
        // 1. Rastrear Colliders (Verde = Sólido, Naranja = Trigger)
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        foreach (var col in colliders) {
            Color colColor = col.isTrigger ? new Color(1f, 0.5f, 0f) : Color.green;

            if (col is BoxCollider2D box) {
                var lr = CreateLineRenderer(box.transform, $"Box_{col.gameObject.name}", colColor);
                _updateActions.Add(() => {
                    if (box != null && lr != null) {
                        lr.transform.localPosition = box.offset;
                        UpdateBoxPoints(lr, box.size);
                    }
                });
            } else if (col is CircleCollider2D circle) {
                var lr = CreateLineRenderer(circle.transform, $"Circle_{col.gameObject.name}", colColor);
                _updateActions.Add(() => {
                    if (circle != null && lr != null) {
                        lr.transform.localPosition = circle.offset;
                        UpdateCirclePoints(lr, circle.radius);
                    }
                });
            }
        }

        // 2. Rastrear Radio de Detección (Cyan)
        var detLr = CreateLineRenderer(transform, "DetectionRadius", Color.cyan);
        _updateActions.Add(() => {
            if (detLr != null) {
                detLr.transform.localPosition = Vector2.zero;
                UpdateCirclePoints(detLr, ClothCombatHandler.DetectionRadiusCloth);
            }
        });

        // 3. Hitbox de ataque (Magenta) - en espacio de mundo, se invierte con la orientación
        var atkLr = CreateLineRenderer(transform, "AttackBox", Color.magenta, true);
        _updateActions.Add(() => {
            if (atkLr != null) {
                Vector2 c = ClothCombatHandler.GetAttackBoxCenter(transform.position, ClothCombatHandler.GetFacing(transform));
                UpdateBoxWorld(atkLr, c, ClothCombatHandler.AttackBoxSize, transform.position.z);
            }
        });
    }

    private void ClearVisualizers() {
        foreach (var v in _visualizers) {
            if (v != null)
                Destroy(v);
        }
        _visualizers.Clear();
        _updateActions.Clear();
    }

    // Configura la base del LineRenderer
    private LineRenderer CreateLineRenderer(Transform parent, string objName, Color color, bool worldSpace = false) {
        GameObject go = new GameObject($"Debug_{objName}");
        go.transform.SetParent(parent, false);
        _visualizers.Add(go);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.material = _lineMaterial;
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.sortingLayerName = "Over";
        lr.sortingOrder = 32767;
        lr.useWorldSpace = worldSpace;

        return lr;
    }

    // Dibuja un círculo basado en un radio
    private void UpdateCirclePoints(LineRenderer lr, float radius) {
        lr.positionCount = CircleSegments + 1;
        for (int i = 0; i <= CircleSegments; i++) {
            float rad = Mathf.Deg2Rad * (i * 360f / CircleSegments);
            lr.SetPosition(i, new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, 0));
        }
    }

    // Dibuja una caja basada en un tamaño (espacio local)
    private void UpdateBoxPoints(LineRenderer lr, Vector2 size) {
        lr.positionCount = 5;
        Vector2 extents = size / 2f;
        lr.SetPosition(0, new Vector3(-extents.x, -extents.y, 0));
        lr.SetPosition(1, new Vector3(-extents.x, extents.y, 0));
        lr.SetPosition(2, new Vector3(extents.x, extents.y, 0));
        lr.SetPosition(3, new Vector3(extents.x, -extents.y, 0));
        lr.SetPosition(4, new Vector3(-extents.x, -extents.y, 0));
    }

    // Dibuja una caja en coordenadas de mundo (para la hitbox de ataque)
    private void UpdateBoxWorld(LineRenderer lr, Vector2 center, Vector2 size, float z) {
        lr.positionCount = 5;
        Vector2 e = size / 2f;
        lr.SetPosition(0, new Vector3(center.x - e.x, center.y - e.y, z));
        lr.SetPosition(1, new Vector3(center.x - e.x, center.y + e.y, z));
        lr.SetPosition(2, new Vector3(center.x + e.x, center.y + e.y, z));
        lr.SetPosition(3, new Vector3(center.x + e.x, center.y - e.y, z));
        lr.SetPosition(4, new Vector3(center.x - e.x, center.y - e.y, z));
    }

    private void OnDestroy() {
        ClearVisualizers();
    }
}
