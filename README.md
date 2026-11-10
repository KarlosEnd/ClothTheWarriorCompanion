# Cloth The Warrior Companion

Un mod para **Hollow Knight** que permite a **Cloth** acompañar y luchar codo a codo junto al Caballero a lo largo de Hallownest.

---

## 🎮 Controles por Defecto

Los atajos pueden ser configurados en el archivo de guardado global del mod:

| Tecla | Acción | Descripción |
| :---: | :--- | :--- |
| `Y` | **Invocar / Retirar** | Hace aparecer o desaparecer a Cloth. |
| `U` | **Reubicar (Recall)** | Reubica a Cloth inmediatamente junto al Caballero. |
| `I` | **Excavar (Burrow)** | Ordena a Cloth excavar y reaparecer cerca del Caballero. |

---

## 📁 Estructura del Proyecto

El código fuente sigue las convenciones estándar de C#/.NET y la arquitectura modular para mods de Hollow Knight:

```text
├── ClothTheWarriorCompanion.sln
├── src/
│   └── ClothTheWarriorCompanion/
│       ├── Behaviors/
│       │   ├── ClothCompanionManager.cs    # Ciclo de vida, invocación y transiciones de escena
│       │   └── ClothFollowBehavior.cs     # IA, física parabólica de saltos y combate
│       ├── Config/
│       │   └── GlobalSettings.cs          # Ajustes globales y configuración de teclas
│       ├── Fsm/
│       │   └── ClothFsmModifier.cs        # Modificaciones a la PlayMaker FSM de Cloth
│       ├── Hooks/
│       │   ├── CompanionInputListener.cs  # Detección de atajos de teclado
│       │   └── EnemyMarker.cs             # Marcado de objetivos prioritarios tras golpes
│       ├── ClothTheWarriorCompanionMod.cs # Entry point del mod (IMod, ITogglableMod)
│       ├── ClothTheWarriorCompanion.csproj
│       └── GlobalUsings.cs
```

---

## ⚡ Optimizaciones Realizadas

- **Cero Asignaciones GC en Combate:** Se utiliza `Physics2D.OverlapCircleNonAlloc` con búfer estático preasignado y reutilización de colecciones para evitar recolección de basura recurrente en bucles de combate.
- **Acceso seguro a Preloads y Diccionarios:** Uso de `TryGetValue` para evitar excepciones `KeyNotFoundException` en cargas de escena.
- **Limpieza de nombres y convenciones:** Estándar C# PascalCase/camelCase, campos privados prefijados con `_`, documentación XML en cada módulo y separación por responsabilidades.
- **Lógica de juego 100% preservada:** Toda la física de salto, caída con `diveMultiplier`, animaciones y transiciones de sala se mantienen idénticas.

