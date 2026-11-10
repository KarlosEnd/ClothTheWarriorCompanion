namespace ClothTheWarriorCompanion.Config;

/// <summary>
/// Configuración global de atajos de teclado para controlar a Cloth.
/// </summary>
public sealed class GlobalSettings {
	[JsonIgnore]
	private string _summonKey = "y";

	[JsonIgnore]
	private string _recallKey = "u";

	[JsonIgnore]
	private string _burrowKey = "i";

	/// <summary>
	/// Tecla para invocar o desinvocar a Cloth.
	/// </summary>
	public string SummonKey {
		get => _summonKey;
		set {
			if (IsValidKey(value)) {
				_summonKey = value;
			}
		}
	}

	/// <summary>
	/// Tecla para reubicar inmediatamente a Cloth junto al Caballero.
	/// </summary>
	public string RecallKey {
		get => _recallKey;
		set {
			if (IsValidKey(value)) {
				_recallKey = value;
			}
		}
	}

	/// <summary>
	/// Tecla para ordenar a Cloth que excave y reaparezca cerca del Caballero.
	/// </summary>
	public string BurrowKey {
		get => _burrowKey;
		set {
			if (IsValidKey(value)) {
				_burrowKey = value;
			}
		}
	}

	private static bool IsValidKey(string value) {
		if (string.IsNullOrWhiteSpace(value)) return false;
		try {
			_ = Input.GetKey(value);
			return true;
		} catch (ArgumentException) {
			return false;
		}
	}
}

