using System;
using UnityEngine;
using ClothTheWarriorCompanion.Config;

namespace ClothTheWarriorCompanion.Behaviors;

/// <summary>
/// Conector nativo con el pipeline de daño e impacto de Hollow Knight (IHitResponder).
/// Permite que Cloth reciba de forma nativa e integrada cualquier ataque del Caballero:
/// clavo básico, artes del clavo (Gran Tajo, Tajo Veloz, Ciclón) y hechizos mágicos
/// (Espíritu Vengativo, Alma Sombría, Salto Desolador, Oscuridad Descendiente, Espectros Aulladores, Chirrido del Abismo).
/// </summary>
public sealed class ClothHitResponder : MonoBehaviour, IHitResponder {
    public void Hit(HitInstance damageInstance) {
        if (!ClothSettings.Current.CanTakeDamage || !ClothSettings.Current.KnightCanHurtCloth)
            return;

        // Ignorar daño proveniente de la propia Cloth
        if (damageInstance.Source != null && damageInstance.Source.transform.IsChildOf(transform))
            return;

        // Enviar evento DEALT DAMAGE al atacante: permite que el Caballero realice pogo de clavo (rebote),
        // genere partículas de impacto y reciba retroalimentación auténtica del juego
        if (damageInstance.Source != null) {
            FSMUtility.SendEventToGameObject(damageInstance.Source, "DEALT DAMAGE");
        }

        var follow = GetComponent<ClothFollowBehavior>();
        if (follow != null) {
            follow.HandleHitInstance(damageInstance);
        }
    }
}

