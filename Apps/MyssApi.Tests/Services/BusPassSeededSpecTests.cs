namespace Myss.Api.Tests.Services
{
    using System.Text.Json;
    using Myss.Api.Domain;
    using Myss.Api.Models;
    using Myss.Api.Services;

    /// <summary>
    /// The bus pass validation as a citizen meets it: the spec validator and the
    /// bus pass rules together, against every bus pass form version exactly as
    /// Strapi seeds it (linked from MyssContent by the csproj). The hand-written
    /// specs in the other suites pin individual rules; this suite pins that the
    /// real forms accept a complete request and refuse the incomplete ones, so a
    /// change to either side cannot strand a citizen on a published version.
    /// </summary>
    public class BusPassSeededSpecTests
    {
        private static readonly DateOnly Today = new(2026, 9, 10);

        public static IEnumerable<object[]> EveryVersion => Enumerable.Range(1, 7).Select(v => new object[] { v });

        public static IEnumerable<object[]> VersionsWithTheReplacementAcknowledgement =>
            Enumerable.Range(4, 4).Select(v => new object[] { v });

        [Theory]
        [MemberData(nameof(EveryVersion))]
        public void ACompleteNewApplication_Passes(int version)
        {
            Assert.Empty(Run(version, NewApplicant(version)));
        }

        [Theory]
        [MemberData(nameof(EveryVersion))]
        public void ACompleteAddressUpdate_Passes(int version)
        {
            Assert.Empty(Run(version, AddressUpdate(version)));
        }

        [Theory]
        [MemberData(nameof(EveryVersion))]
        public void ACompleteReplacementRequest_Passes(int version)
        {
            Assert.Empty(Run(version, Replacement(version)));
        }

        [Theory]
        [MemberData(nameof(EveryVersion))]
        public void ABirthMonthFormIoSendsAsANumber_Passes(int version)
        {
            // Form.io turns the option "12" into the number 12 before posting.
            // A citizen born in October, November or December must still get
            // through.
            foreach (int month in new[] { 10, 11, 12 })
            {
                var answers = NewApplicant(version);
                answers["birthMonth"] = month;

                Assert.Empty(Run(version, answers));
            }
        }

        [Theory]
        [MemberData(nameof(EveryVersion))]
        public void ANewApplicantWithoutTheEligibilityCategory_IsRefusedOnce(int version)
        {
            var answers = NewApplicant(version);
            answers.Remove("eligibilityCategory");

            ValidationErrorModel error = Assert.Single(Run(version, answers));
            Assert.Equal("eligibilityCategory", error.Field);
            Assert.Equal(ValidationKeywords.FieldRequired, error.Keyword);
        }

        [Theory]
        [MemberData(nameof(EveryVersion))]
        public void ANewApplicantWhoDoesNotAcknowledgeTheCriteria_IsRefusedOnce(int version)
        {
            foreach (object? unticked in new object?[] { false, null })
            {
                var answers = NewApplicant(version);
                if (unticked is null)
                {
                    answers.Remove("eligibilityAcknowledged");
                }
                else
                {
                    answers["eligibilityAcknowledged"] = unticked;
                }

                ValidationErrorModel error = Assert.Single(Run(version, answers));
                Assert.Equal("eligibilityAcknowledged", error.Field);
                Assert.Equal(ValidationKeywords.FieldRequired, error.Keyword);
            }
        }

        [Theory]
        [MemberData(nameof(VersionsWithTheReplacementAcknowledgement))]
        public void AReplacementWithoutTheCancellationAcknowledgement_IsRefusedOnce(int version)
        {
            foreach (object? unticked in new object?[] { false, null })
            {
                var answers = Replacement(version);
                if (unticked is null)
                {
                    answers.Remove("acknowledgedPassCancellation");
                }
                else
                {
                    answers["acknowledgedPassCancellation"] = unticked;
                }

                ValidationErrorModel error = Assert.Single(Run(version, answers));
                Assert.Equal("acknowledgedPassCancellation", error.Field);
                Assert.Equal(ValidationKeywords.FieldRequired, error.Keyword);
            }
        }

        [Theory]
        [MemberData(nameof(EveryVersion))]
        public void ADifferentMailingAddressWithMissingParts_IsRefusedPerPart(int version)
        {
            var answers = NewApplicant(version);
            answers["mailingAddressDifferent"] = "yes";
            answers["mailingStreetAddress1"] = "PO Box 1";

            IReadOnlyList<ValidationErrorModel> errors = Run(version, answers);

            Assert.Equal(
                ["mailingCity", "mailingProvince", "mailingPostalCode"],
                errors.Select(e => e.Field).ToArray());
            Assert.All(errors, e => Assert.Equal(ValidationKeywords.FieldRequired, e.Keyword));
        }

        [Theory]
        [MemberData(nameof(EveryVersion))]
        public void AnImpossibleDateOfBirth_IsRefusedOnceOnTheDayField(int version)
        {
            // v6 checks the group on the form (dateParts) and the bus pass rules
            // check it too; the citizen must read one reason, not two.
            var answers = NewApplicant(version);
            answers["birthDay"] = "31";
            answers["birthMonth"] = "02";

            ValidationErrorModel error = Assert.Single(Run(version, answers));
            Assert.Equal("birthDay", error.Field);
        }

        [Fact]
        public void V6_AnEmptyEmailVerification_IsRefusedOnTheVerificationField()
        {
            var answers = NewApplicant(6);
            answers.Remove("emailVerification");

            ValidationErrorModel error = Assert.Single(Run(6, answers));
            Assert.Equal("emailVerification", error.Field);
            Assert.Equal(ValidationKeywords.EmailMismatch, error.Keyword);
        }

        [Fact]
        public void V6_WordsTheConditionallyRequiredFieldsAsTheLegacyRulesDid()
        {
            var answers = NewApplicant(6);
            answers["mailingAddressDifferent"] = "yes";

            IReadOnlyList<ValidationErrorModel> errors = Run(6, answers);

            Assert.Equal("A city is required", Assert.Single(errors, e => e.Field == "mailingCity").Message);
            Assert.Equal("A postal code is required", Assert.Single(errors, e => e.Field == "mailingPostalCode").Message);
        }

        [Fact]
        public void V6AndV7_UseTheSharedPhoneRuleBeforeDispatch()
        {
            var earlierVersion = NewApplicant(6);
            earlierVersion["phoneNumber"] = "1 (250) 555-0199";
            ValidationErrorModel earlierError = Assert.Single(Run(6, earlierVersion));
            Assert.Equal(ValidationKeywords.PhoneInvalidFormat, earlierError.Keyword);
            Assert.Equal("Phone number is invalid", earlierError.Message);

            foreach (string accepted in new[] { "2501550199", "(250) 055-0199" })
            {
                var answers = NewApplicant(7);
                answers["phoneNumber"] = accepted;
                Assert.Empty(Run(7, answers));
            }

            foreach (string refused in new[] { "(123) 456-7890", "1 (250) 555-0199", "(250) 555-0199 x12", "25055501999", "" })
            {
                var answers = NewApplicant(7);
                answers["phoneNumber"] = refused;
                ValidationErrorModel error = Assert.Single(Run(7, answers));
                Assert.Equal("phoneNumber", error.Field);
                Assert.Equal(refused == "" ? ValidationKeywords.FieldRequired : ValidationKeywords.PhoneInvalidFormat, error.Keyword);
                Assert.Equal("Phone number is invalid", error.Message);
            }
        }

        private static IReadOnlyList<ValidationErrorModel> Run(int version, Dictionary<string, object?> answers)
        {
            using JsonDocument spec = JsonDocument.Parse(File.ReadAllText(SpecPath(version)));
            using JsonDocument doc = JsonSerializer.SerializeToDocument(answers);
            JsonElement root = doc.RootElement.Clone();

            IReadOnlyList<ValidationErrorModel> specErrors = FormSpecValidator.Validate(spec.RootElement, root);
            IReadOnlyList<ValidationErrorModel> ruleErrors = BusPassRules.Validate(root, Today, version);
            return FormSpecValidator.OnePerField([.. specErrors, .. ruleErrors]);
        }

        private static string SpecPath(int version)
        {
            string file = version == 1 ? "bus-pass-form.json" : $"bus-pass-form-v{version}.json";
            string path = Path.Combine(AppContext.BaseDirectory, "bus-pass-specs", file);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "The seeded bus pass specs were not copied beside the test assembly. " +
                    "Check the <Content Include=\"..\\MyssContent\\src\\lib\\bus-pass-specs\\*.json\"> " +
                    "item in MyssApi.Tests.csproj.",
                    path);
            }

            return path;
        }

        /// <summary>Everything every version asks of an applicant, in the version's own shape.</summary>
        private static Dictionary<string, object?> Common() => new()
        {
            // Digits only: v1 still checked the SIN's shape with a pattern on the
            // form, before the server-side rule took over in v2.
            ["socialInsuranceNumber"] = "046454286",
            ["firstName"] = "Ada",
            ["lastName"] = "Lovelace",
            ["birthDay"] = "10",
            ["birthMonth"] = "12",
            ["birthYear"] = "1950",
            ["phoneNumber"] = "(250) 555-0199",
            ["phoneType"] = "home",
            ["leaveMessage"] = true,
            ["email"] = "ada@example.com",
            ["emailVerification"] = "ada@example.com",
            ["preferredCommunication"] = "phone",
            ["streetAddress1"] = "501 Belleville St",
            ["city"] = "Victoria",
            ["province"] = "BC",
            // No space: the postal code pattern allowed one only from v5.
            ["postalCode"] = "V8V1X4",
            ["mailingAddressDifferent"] = "no",
            ["submit"] = true,
        };

        private static Dictionary<string, object?> NewApplicant(int version)
        {
            var answers = Common();
            if (version < 3)
            {
                answers["applicantCategory"] = "new";
            }
            else
            {
                answers["serviceRequestType"] = "newApplication";
            }

            answers["eligibilityAcknowledged"] = true;
            answers["eligibilityCategory"] = "over65";
            return answers;
        }

        private static Dictionary<string, object?> AddressUpdate(int version)
        {
            var answers = Common();
            if (version < 3)
            {
                answers["applicantCategory"] = "existing";
                answers["existingClientReason"] = "moved";
            }
            else
            {
                answers["serviceRequestType"] = "addressUpdate";
            }

            return answers;
        }

        private static Dictionary<string, object?> Replacement(int version)
        {
            var answers = Common();
            if (version < 3)
            {
                answers["applicantCategory"] = "existing";
                answers["existingClientReason"] = "replacement";
            }
            else
            {
                answers["serviceRequestType"] = "replacement";
            }

            if (version >= 4)
            {
                answers["acknowledgedPassCancellation"] = true;
            }

            return answers;
        }
    }
}
