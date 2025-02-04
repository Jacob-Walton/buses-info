// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;
[assembly: SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Will be written to.", Scope = "member", Target = "~P:BusInfo.Models.ApplicationUser.PreferredRoutes")]
[assembly: SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Will be written to.", Scope = "member", Target = "~P:BusInfo.Models.BusStatus.PredictedBays")]
[assembly: SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Will be written to.", Scope = "member", Target = "~P:BusInfo.Models.BusInfoResponse.BusData")]
[assembly: SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Will be written to.", Scope = "member", Target = "~P:BusInfo.Models.BusInfoLegacyResponse.BusData")]
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "<Pending>", Scope = "member", Target = "~M:BusInfo.Program.LoadAndConfigureServices(Microsoft.AspNetCore.Builder.WebApplicationBuilder)")]
