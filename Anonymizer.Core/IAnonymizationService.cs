namespace Anonymizer.Core;

public interface IAnonymizationService
{
    AnonymizationResult Anonymize(string text);
}
