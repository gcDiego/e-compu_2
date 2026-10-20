using Front.Web.Models;
using System.Net.Http.Json;

namespace Front.Web.Services;

public sealed class LocationApiClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<StateDto>> GetStatesAsync(CancellationToken ct) =>
        await httpClient.GetFromJsonAsync<List<StateDto>>("api/location/states", ct) ?? [];

    public async Task<IReadOnlyList<MunicipalityDto>> GetMunicipalitiesAsync(string stateId, CancellationToken ct)
    {
        var response = await httpClient.GetAsync($"api/location/municipalities/{stateId}", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<MunicipalityDto>>(ct) ?? []
            : [];
    }

    public async Task<IReadOnlyList<LocalityDto>> GetLocalitiesAsync(string municipalityId, CancellationToken ct)
    {
        var response = await httpClient.GetAsync($"api/location/localities/{municipalityId}", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<LocalityDto>>(ct) ?? []
            : [];
    }
}
