using Clipper.Core;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;

namespace Clipper.YouTube;

/// <summary>
/// Вход в Google. При первом входе открывается браузер, пользователь разрешает доступ,
/// а токен сохраняется в TokenDir — дальше загрузка работает без браузера.
/// </summary>
public sealed class YouTubeAuth
{
    private const string UserKey = "user";

    private static readonly string[] Scopes =
    {
        YouTubeService.Scope.YoutubeUpload,   // загрузка видео
        YouTubeService.Scope.YoutubeReadonly, // имя канала, чтобы показать «вы вошли как …»
    };

    private readonly YouTubeOptions _options;

    public YouTubeAuth(ClipperOptions options) : this(options.YouTube) { }

    public YouTubeAuth(YouTubeOptions options) => _options = options;

    private FileDataStore Store => new(Path.GetFullPath(_options.TokenDir), fullPath: true);

    /// <summary>Есть ли сохранённый вход (без открытия браузера).</summary>
    public async Task<bool> IsSignedInAsync() =>
        await Store.GetAsync<TokenResponse>(UserKey) is not null;

    /// <summary>Вход через браузер. Возвращает название YouTube-канала.</summary>
    public async Task<string> SignInAsync(CancellationToken cancellationToken)
    {
        using var service = new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = await AuthorizeAsync(cancellationToken),
            ApplicationName = "Clipper",
        });
        var request = service.Channels.List("snippet");
        request.Mine = true;
        var response = await request.ExecuteAsync(cancellationToken);
        return response.Items?.FirstOrDefault()?.Snippet.Title
            ?? throw new InvalidOperationException("У этого Google-аккаунта нет YouTube-канала.");
    }

    public Task SignOutAsync() => Store.ClearAsync();

    /// <summary>Сервис YouTube для загрузки. Требует, чтобы вход уже был выполнен.</summary>
    public async Task<YouTubeService> CreateServiceAsync(CancellationToken cancellationToken)
    {
        if (!await IsSignedInAsync())
            throw new InvalidOperationException("Не выполнен вход в Google: нажмите «Войти через Google» в настройках.");

        return new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = await AuthorizeAsync(cancellationToken),
            ApplicationName = "Clipper",
        });
    }

    private async Task<UserCredential> AuthorizeAsync(CancellationToken cancellationToken)
    {
        var secretsPath = Path.GetFullPath(_options.ClientSecretsPath);
        if (!File.Exists(secretsPath))
            throw new FileNotFoundException(
                $"Не найден файл ключа Google {secretsPath}. Как его получить — в README, раздел YouTube Shorts.");

        await using var stream = File.OpenRead(secretsPath);
        var secrets = (await GoogleClientSecrets.FromStreamAsync(stream, cancellationToken)).Secrets;
        return await GoogleWebAuthorizationBroker.AuthorizeAsync(secrets, Scopes, UserKey, cancellationToken, Store);
    }
}
