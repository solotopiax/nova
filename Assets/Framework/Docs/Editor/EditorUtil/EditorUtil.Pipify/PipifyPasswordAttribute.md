# PipifyPasswordAttribute

| Item | Value |
|---|---|
| Class | `[AttributeUsage(Field)] public sealed class PipifyPasswordAttribute : Attribute` |
| Namespace | `NovaFramework.Editor` |
| File | `EditorUtil.Pipify/Definitions/PipifyPasswordAttribute.cs` |

Marks a `string` parameter field so `PipifyWindow` renders it with `EditorGUI.PasswordField`.

This only changes editor display. The value is still stored as a normal string in `PipifySettingsSO`, and CLI overrides still use the same field name.

## Example

```csharp
[PipifyPassword]
public string AndroidKeystorePass;
```

## Related

- [EditorUtil.Pipify.md](./EditorUtil.Pipify.md)
- [PipifySteps.md](./PipifySteps.md)
