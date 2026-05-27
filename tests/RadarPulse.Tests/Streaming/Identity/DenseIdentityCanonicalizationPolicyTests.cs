using System.Text;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Streaming;

public sealed class DenseIdentityCanonicalizationPolicyTests
{
    [Fact]
    public void RadarCodePolicyRequiresExactlyFourUppercaseAsciiLettersOrDigits()
    {
        var catalog = new DenseIdentityCatalog("radar", DenseIdentityCanonicalizationPolicy.RadarCode);

        Assert.Equal(4, catalog.MinimumTextLength);
        Assert.Equal(4, catalog.MaximumTextLength);
        Assert.Equal(0, catalog.GetOrAdd("KTLX"));

        Assert.False(catalog.TryGetId("KTL", out _));
        Assert.False(catalog.TryGetId("KTLX1", out _));
        Assert.False(catalog.TryGetId("ktlx", out _));
        Assert.False(catalog.TryGetId("KTL_", out _));

        Assert.Equal(DenseIdentityValidationError.TooShort, catalog.Validate("KTL").Error);
        Assert.Equal(DenseIdentityValidationError.TooLong, catalog.Validate("KTLX1").Error);
        Assert.Equal(DenseIdentityValidationError.InvalidCharacter, catalog.Validate("ktlx").Error);
        Assert.Equal(DenseIdentityValidationError.InvalidCharacter, catalog.Validate("KTL_").Error);
        Assert.Equal(1, catalog.Count);
    }

    [Fact]
    public void MomentNamePolicyAllowsCompactUppercaseDigitsAndUnderscore()
    {
        var catalog = new DenseIdentityCatalog("moment", DenseIdentityCanonicalizationPolicy.MomentName);

        Assert.Equal(1, catalog.MinimumTextLength);
        Assert.Equal(8, catalog.MaximumTextLength);
        Assert.Equal(0, catalog.GetOrAdd("REF_1"));

        Assert.False(catalog.TryGetId("ref", out _));
        Assert.False(catalog.TryGetId("REF ", out _));
        Assert.False(catalog.TryGetId("TOO_LONG1", out _));

        Assert.Equal(DenseIdentityValidationError.InvalidCharacter, catalog.Validate("ref").Error);
        Assert.Equal(DenseIdentityValidationError.InvalidCharacter, catalog.Validate("REF ").Error);
        Assert.Equal(DenseIdentityValidationError.TooLong, catalog.Validate("TOO_LONG1").Error);
        Assert.Equal(1, catalog.Count);
    }

    [Fact]
    public void PoliciesDoNotTrimOrFoldCase()
    {
        var policy = DenseIdentityCanonicalizationPolicy.RadarCode;
        var catalog = new DenseIdentityCatalog("radar", policy);

        Assert.False(policy.TrimsInput);
        Assert.False(policy.FoldsCase);
        Assert.Throws<ArgumentException>(() => catalog.GetOrAdd("ktlx"));
        Assert.Throws<ArgumentException>(() => catalog.GetOrAdd(" KTLX"));
        Assert.Throws<ArgumentException>(() => catalog.GetOrAdd("KTLX "));
        Assert.Equal(0, catalog.Count);
    }

    [Fact]
    public void DifferentDimensionsCanUseDifferentRules()
    {
        var radarCatalog = new DenseIdentityCatalog("radar", DenseIdentityCanonicalizationPolicy.RadarCode);
        var momentCatalog = new DenseIdentityCatalog("moment", DenseIdentityCanonicalizationPolicy.MomentName);

        Assert.False(radarCatalog.TryGetId("REF_1", out _));
        Assert.Equal(0, momentCatalog.GetOrAdd("REF_1"));
    }

    [Fact]
    public void ValidationResultCapturesReasonPositionAndInputKind()
    {
        var catalog = new DenseIdentityCatalog("moment", DenseIdentityCanonicalizationPolicy.MomentName);

        var textValidation = catalog.Validate("REF ");
        var utf8Validation = catalog.Validate(Encoding.UTF8.GetBytes("REF "));
        byte[] nonAsciiUtf8 = [0xD0, 0xAE];
        var nonAsciiValidation = catalog.Validate(nonAsciiUtf8);

        Assert.False(textValidation.IsValid);
        Assert.Equal(DenseIdentityValidationError.InvalidCharacter, textValidation.Error);
        Assert.Equal(3, textValidation.Position);
        Assert.Equal(' ', textValidation.InvalidValue);
        Assert.Equal(DenseIdentityValidationInputKind.Text, textValidation.InputKind);

        Assert.False(utf8Validation.IsValid);
        Assert.Equal(DenseIdentityValidationInputKind.Utf8Bytes, utf8Validation.InputKind);
        Assert.Equal(3, utf8Validation.Position);
        Assert.Equal((byte)' ', utf8Validation.InvalidValue);

        Assert.False(nonAsciiValidation.IsValid);
        Assert.Equal(DenseIdentityValidationError.InvalidCharacter, nonAsciiValidation.Error);
        Assert.Equal(DenseIdentityValidationInputKind.Utf8Bytes, nonAsciiValidation.InputKind);
        Assert.Equal(0, nonAsciiValidation.Position);
        Assert.Equal(0xD0, nonAsciiValidation.InvalidValue);
    }

    [Fact]
    public void ExceptionMessageIncludesCatalogDimensionAndReason()
    {
        var catalog = new DenseIdentityCatalog("radar", DenseIdentityCanonicalizationPolicy.RadarCode);

        var exception = Assert.Throws<ArgumentException>(() => catalog.GetOrAdd("KTL_"));

        Assert.Contains("radar", exception.Message, StringComparison.Ordinal);
        Assert.Contains("radar code", exception.Message, StringComparison.Ordinal);
        Assert.Contains("invalid value", exception.Message, StringComparison.Ordinal);
        Assert.Contains("position 3", exception.Message, StringComparison.Ordinal);
        Assert.Contains("A-Z", exception.Message, StringComparison.Ordinal);
    }
}
