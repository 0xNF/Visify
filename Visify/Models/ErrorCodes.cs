namespace Visify.Models {
    public enum ErrorCodes {
        NoError = 0,
        MiscFailure = 1,
        DatabaseConnectionFailure = 2,
        DatabaseWriteError = 3,
        DatabaseRetrievalError = 4,
        CouldNotEstablishConnectionToSpotfiyError = 5,
        RefreshTokenNotValidError = 6,
        AccessTokenExpiredError = 7,
        NoUserByThatNameError = 8,
        MiscSpotifyError = 9,
    }
}
