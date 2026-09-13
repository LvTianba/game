namespace BorderValley.Core.Boot
{
    public interface IPreflightCheck
    {
        bool Validate(out string error);
    }
}
