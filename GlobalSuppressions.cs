// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;
[assembly: SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Will be written to.", Scope = "member", Target = "~P:BusInfo.Models.ApplicationUser.PreferredRoutes")]
[assembly: SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Will be written to.", Scope = "member", Target = "~P:BusInfo.Models.BusInfoResponse.BusData")]
[assembly: SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Will be written to.", Scope = "member", Target = "~P:BusInfo.Models.BusInfoLegacyResponse.BusData")]
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Leads to early disposal of information", Scope = "member", Target = "~M:BusInfo.Program.LoadAndConfigureServices(Microsoft.AspNetCore.Builder.WebApplicationBuilder)")]
[assembly: SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "List is required over Collection for EF", Scope = "member", Target = "~P:BusInfo.Models.ApplicationUser.PreferredRoutes")]
[assembly: SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "List is required over Collection for EF", Scope = "member", Target = "~P:BusInfo.Models.ApplicationUser.RecoveryCodes")]
[assembly: SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Not read only", Scope = "member", Target = "~P:BusInfo.Models.ApplicationUser.RecoveryCodes")]
[assembly: SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "List is required for EF", Scope = "member", Target = "~P:BusInfo.Models.PredictionInfo.Predictions")]
[assembly: SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Will be written to.", Scope = "member", Target = "~P:BusInfo.Models.PredictionInfo.Predictions")]
[assembly: SuppressMessage("Style", "IDE0046:Convert to conditional expression", Justification = "Readability", Scope = "member", Target = "~M:BusInfo.Program.CreateAzureCredential(Microsoft.Extensions.Configuration.ConfigurationManager)~Azure.Identity.ClientSecretCredential")]
