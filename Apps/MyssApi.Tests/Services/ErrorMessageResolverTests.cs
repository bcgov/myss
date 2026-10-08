namespace Myss.Api.Tests.Services
{
    using Myss.Api.Domain;
    using Myss.Api.Models;
    using Myss.Api.Services;

    /// <summary>
    /// Tests for <see cref="ErrorMessageResolver"/>.
    /// </summary>
    public class ErrorMessageResolverTests
    {
        [Fact]
        public void Resolve_ReplacesTheMessage_WhereTheCatalogueHasTheKeyword()
        {
            ValidationErrorModel[] errors =
            [
                Error("sin", ValidationKeywords.SinInvalidChecksum, "compiled wording"),
            ];
            var catalogue = new Dictionary<string, string>
            {
                [ValidationKeywords.SinInvalidChecksum] = "authored wording",
            };

            IReadOnlyList<ValidationErrorModel> resolved = ErrorMessageResolver.Resolve(errors, catalogue);

            ValidationErrorModel only = Assert.Single(resolved);
            Assert.Equal("sin", only.Field);
            Assert.Equal(ValidationKeywords.SinInvalidChecksum, only.Keyword);
            Assert.Equal("authored wording", only.Message);
        }

        [Fact]
        public void Resolve_KeepsTheValidatorsWording_WhereTheCatalogueHasNoRow()
        {
            // FORM.FIELD.REQUIRED and friends carry field-specific text that a
            // catalogue row would flatten, so an absent row means "leave it".
            ValidationErrorModel[] errors =
            [
                Error("eligibilityAcknowledged", ValidationKeywords.FieldRequired, "Acknowledgement is required"),
            ];

            IReadOnlyList<ValidationErrorModel> resolved = ErrorMessageResolver.Resolve(
                errors,
                new Dictionary<string, string>());

            Assert.Equal("Acknowledgement is required", Assert.Single(resolved).Message);
        }

        [Fact]
        public void Resolve_KeepsTheValidatorsWording_WhereTheRowIsBlank()
        {
            // A row with no text must not blank a citizen's message.
            ValidationErrorModel[] errors =
            [
                Error("sin", ValidationKeywords.SinWrongLength, "compiled wording"),
            ];
            var catalogue = new Dictionary<string, string>
            {
                [ValidationKeywords.SinWrongLength] = "   ",
            };

            IReadOnlyList<ValidationErrorModel> resolved = ErrorMessageResolver.Resolve(errors, catalogue);

            Assert.Equal("compiled wording", Assert.Single(resolved).Message);
        }

        [Fact]
        public void Resolve_PreservesOrder_AndDoesNotModifyTheInput()
        {
            // Order is what the error summary renders in, and the input belongs
            // to the validator that produced it.
            ValidationErrorModel first = Error("a", ValidationKeywords.SinWrongLength, "one");
            ValidationErrorModel second = Error("b", ValidationKeywords.EmailInvalidFormat, "two");
            ValidationErrorModel third = Error("c", ValidationKeywords.FieldRequired, "three");
            var catalogue = new Dictionary<string, string>
            {
                [ValidationKeywords.SinWrongLength] = "ONE",
                [ValidationKeywords.EmailInvalidFormat] = "TWO",
            };

            IReadOnlyList<ValidationErrorModel> resolved = ErrorMessageResolver.Resolve([first, second, third], catalogue);

            Assert.Equal(["a", "b", "c"], resolved.Select(e => e.Field));
            Assert.Equal(["ONE", "TWO", "three"], resolved.Select(e => e.Message));
            Assert.Equal("one", first.Message);
            Assert.Equal("two", second.Message);
            Assert.Same(third, resolved[2]);
        }

        [Fact]
        public void Resolve_EmptyErrors_ReturnsEmpty()
        {
            Assert.Empty(ErrorMessageResolver.Resolve([], new Dictionary<string, string>()));
        }

        [Fact]
        public void Resolve_LeavesWordingTheFormAuthored_EvenWhenTheCatalogueHasTheKeyword()
        {
            // A designer's per-field wording (errors.sin, validate.customMessage)
            // is more specific than the catalogue's generic row and must win.
            ValidationErrorModel[] errors =
            [
                new()
                {
                    Field = "sin",
                    Keyword = ValidationKeywords.SinInvalidChecksum,
                    Message = "Enter the SIN shown on your card",
                    HasAuthoredMessage = true,
                },
            ];
            var catalogue = new Dictionary<string, string>
            {
                [ValidationKeywords.SinInvalidChecksum] = "catalogue wording",
            };

            IReadOnlyList<ValidationErrorModel> resolved = ErrorMessageResolver.Resolve(errors, catalogue);

            Assert.Equal("Enter the SIN shown on your card", Assert.Single(resolved).Message);
        }

        private static ValidationErrorModel Error(string field, string keyword, string message) =>
            new() { Field = field, Keyword = keyword, Message = message };
    }
}
