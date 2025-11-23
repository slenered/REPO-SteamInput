using System;
using Steamworks.Data;

namespace REPO_SteamInput.Data;

public readonly struct ActionHandle : IEquatable<ActionHandle> {
	public InputDigitalActionHandle_t DigitalActionHandle { get; }

	public InputAnalogActionHandle_t AnalogActionHandle { get; }

	public ActionHandle(InputDigitalActionHandle_t handle) {
		DigitalActionHandle = handle;
		IsDigitalAction = true;
	}
	public ActionHandle(InputAnalogActionHandle_t handle) {
		AnalogActionHandle = handle;
		IsAnalogAction = true;
	}

	public bool IsDigitalAction { get; } = false;
	public bool IsAnalogAction { get; } = false;

	public bool Equals(ActionHandle other) {
		return DigitalActionHandle.Equals(other.DigitalActionHandle) && AnalogActionHandle.Equals(other.AnalogActionHandle);
	}

	public override bool Equals(object? obj) {
		return obj is ActionHandle other && Equals(other);
	}

	public override int GetHashCode() {
		return HashCode.Combine(DigitalActionHandle, AnalogActionHandle);
	}
}