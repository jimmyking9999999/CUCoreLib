public class SettingString : Setting
{
    public string value;

    public override object GetValue()
    {
        return value;
    }

    public override void SetValue(object val)
    {
        value = val?.ToString() ?? string.Empty;
    }
}
