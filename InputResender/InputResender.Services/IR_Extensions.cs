namespace InputResender.Services;
public static class IR_Extensions {
	public static bool IsModifier ( this KeyCode key ) => (int)(key & KeyCode.Modifiers) > 1;
}