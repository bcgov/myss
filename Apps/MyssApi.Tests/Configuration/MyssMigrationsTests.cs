namespace Myss.Api.Tests.Configuration
{
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Myss.Api.Data;
    using Myss.Data;
    using Myss.Data.Intake;
    using Myss.Data.Platform;

    /// <summary>
    /// The forms, platform and intake contexts are defined in MyssData but their
    /// migrations live in this API. These pin that every context the API registers
    /// finds its own migrations, without a database: EF lists them from the
    /// assembly it was pointed at. A context left pointing at MyssData would list
    /// none, and <c>--migrate</c> and <c>dotnet ef</c> would then do nothing.
    /// </summary>
    public class MyssMigrationsTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;

        /// <summary>Initializes a new instance of the <see cref="MyssMigrationsTests"/> class.</summary>
        /// <param name="factory">The injected in-memory host factory.</param>
        public MyssMigrationsTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData(typeof(FormsDbContext), "20260729181333_InitialForms")]
        [InlineData(typeof(AttachmentsDbContext), "20260814160853_InitialAttachments")]
        [InlineData(typeof(PlatformDbContext), "20260928213107_InitialPlatform")]
        [InlineData(typeof(IntakeDbContext), "20260928213110_InitialIntake")]
        public void EveryRegisteredContext_FindsItsMigrationsInThisAssembly(Type contextType, string firstMigration)
        {
            using IServiceScope scope = _factory.Services.CreateScope();
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);

            IEnumerable<string> migrations = context.Database.GetMigrations();

            Assert.Equal(firstMigration, migrations.First());
        }
    }
}
