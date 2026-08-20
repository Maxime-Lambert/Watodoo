using Xunit;

// Course statique dans Hangfire.PostgreSql.PostgreSqlDistributedLock entre plusieurs
// WebApplicationFactory<Program> (chacun démarre un vrai HangfireServer, jamais désactivé en test)
// démarrés/arrêtés en parallèle : rendue quasi systématique par les fixtures fonctionnelles
// concurrentes de ce projet (FunctionalTestFixture, AuthRateLimitingTestFixture,
// SeedGamesFromIgdbTestFixture), pré-existante mais jusque-là rare avec moins de hosts concurrents.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
