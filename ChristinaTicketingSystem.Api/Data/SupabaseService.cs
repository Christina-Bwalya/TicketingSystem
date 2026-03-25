namespace ChristinaTicketingSystem.Api.Data;

/// <summary>
/// Thin wrapper that exposes the initialized Supabase client to the rest of the app.
/// </summary>
public class SupabaseService
{
    public Supabase.Client Client { get; }

    public SupabaseService(Supabase.Client client)
    {
        Client = client;
    }
}
