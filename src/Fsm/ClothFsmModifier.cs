namespace ClothTheWarriorCompanion.Fsm;

/// <summary>
/// Modifica la máquina de estados PlayMaker original de Cloth cuando se inicializa.
/// </summary>
internal static class ClothFsmModifier {
	private const string ControlFsmName = "Control";
	private const string CompanionGameObjectName = "Cloth Fighter Companion";

	internal static void OnFsmEnableHook(On.PlayMakerFSM.orig_OnEnable orig, PlayMakerFSM self) {
		orig(self);

		if (self != null && self.FsmName == ControlFsmName && self.gameObject.name == CompanionGameObjectName) {
			ConfigureCompanionFsm(self);
			ClothTheWarriorCompanionMod.Instance?.LogDebug("Cloth Fighter Companion FSM successfully configured");
		}
	}

	internal static void ConfigureCompanionFsm(PlayMakerFSM fsm) {
		var readyState = fsm.Fsm.GetState("Ready");
		var idleState = fsm.Fsm.GetState("Idle");
		var cdState = fsm.CreateState("Cooldown");

		var cancelEvent = fsm.FsmEvents.First(e => e.Name == "CANCEL");

		fsm.ChangeTransition("Init", "FINISHED", "Ready");
		idleState.AddTransition(cancelEvent, "Cooldown");
		cdState.AddTransition(FsmEvent.Finished, "Idle");

		readyState.InsertAction(0, fsm.GetAction<SetMeshRenderer>("Enter", 0));
		readyState.InsertAction(1, fsm.GetAction<SetGravity2dScale>("Enter", 2));
		readyState.InsertAction(2, fsm.GetAction<SetBoxColliderTrigger>("Collider On", 0));
		readyState.InsertAction(3, fsm.GetAction<Tk2dPlayAnimation>("Idle Wait", 0));

		idleState.RemoveAction(0);
		idleState.InsertAction(3, new GameObjectIsNull {
			gameObject = fsm.FsmVariables.FindFsmGameObject("Boss"),
			isNull = cancelEvent
		});

		cdState.AddAction(new Wait {
			time = 0.1f,
			finishEvent = FsmEvent.Finished
		});
	}
}

