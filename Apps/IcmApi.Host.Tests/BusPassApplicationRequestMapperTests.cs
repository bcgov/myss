namespace Icm.Api.Host.Tests
{
    using Icm.Api.Host.Contracts;
    using Icm.Api.Host.Services;
    using Icm.Api.Models;

    public class BusPassApplicationRequestMapperTests
    {
        [Fact]
        public void MapsEveryField()
        {
            var request = new BusPassApplicationRequest
            {
                SubmissionKey = "  abc-123  ",
                RequestType = BusPassRequestType.Replacement,
                ApplicantType = BusPassApplicantType.FirstNations,
                AcknowledgedPassCancellation = true,
                AcknowledgedEligibilityCriteria = false,
                SocialInsuranceNumber = "046454286",
                BusPassAccountNumber = "12345678",
                FirstName = "Myss",
                LastName = "IntegrationTest",
                DateOfBirth = new DateOnly(1950, 1, 1),
                PhoneNumber = "2505550199",
                PhoneType = BusPassPhoneType.Work,
                LeaveMessageAllowed = true,
                EmailAddress = "myss.integrationtest@example.com",
                PreferredContactMethod = BusPassContactMethod.Email,
                ResidentialAddress = new BusPassAddressRequest { Unit = "1", Line1 = "501 Belleville St", Line2 = "Rear", City = "Victoria", Province = "BC", PostalCode = "V8V 1X4" },
                MailingAddress = new BusPassAddressRequest { Line1 = "PO Box 9041", City = "Victoria", Province = "BC", PostalCode = "V8W 9E1" },
            };

            BusPassApplication application = BusPassApplicationRequestMapper.ToApplication(request);

            Assert.Equal("abc-123", application.SubmissionKey);
            Assert.Equal(BusPassRequestType.Replacement, application.RequestType);
            Assert.Equal(BusPassApplicantType.FirstNations, application.ApplicantType);
            Assert.True(application.AcknowledgedPassCancellation);
            Assert.False(application.AcknowledgedEligibilityCriteria);
            Assert.Equal("046454286", application.SocialInsuranceNumber);
            Assert.Equal("12345678", application.BusPassAccountNumber);
            Assert.Equal("Myss", application.FirstName);
            Assert.Equal("IntegrationTest", application.LastName);
            Assert.Equal(new DateOnly(1950, 1, 1), application.DateOfBirth);
            Assert.Equal("2505550199", application.PhoneNumber);
            Assert.Equal(BusPassPhoneType.Work, application.PhoneType);
            Assert.True(application.LeaveMessageAllowed);
            Assert.Equal("myss.integrationtest@example.com", application.EmailAddress);
            Assert.Equal(BusPassContactMethod.Email, application.PreferredContactMethod);
            Assert.Equal("Rear", application.ResidentialAddress?.Line2);
            Assert.Equal("1", application.ResidentialAddress?.Unit);
            Assert.Equal("PO Box 9041", application.MailingAddress?.Line1);
            Assert.Null(application.MailingAddress?.Unit);
            Assert.Null(application.Attachments);
        }

        [Fact]
        public void BlankSubmissionKeyAndAbsentAddresses_MapToNull()
        {
            var request = new BusPassApplicationRequest { SubmissionKey = "   ", RequestType = BusPassRequestType.NewApplication };

            BusPassApplication application = BusPassApplicationRequestMapper.ToApplication(request);

            Assert.Null(application.SubmissionKey);
            Assert.Null(application.ResidentialAddress);
            Assert.Null(application.MailingAddress);
        }
    }
}
