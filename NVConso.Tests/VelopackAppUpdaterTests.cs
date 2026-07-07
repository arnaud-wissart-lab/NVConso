using Velopack.Exceptions;

namespace NVConso.Tests
{
    public class VelopackAppUpdaterTests
    {
        [Fact]
        public void HandleException_ShouldReturnReadableNetworkMessage()
        {
            AppUpdateOperationResult result = VelopackAppUpdater.HandleException(
                new HttpRequestException("Name resolution failed."),
                "vérification de mise à jour");

            Assert.False(result.Success);
            Assert.Equal(AppUpdateStatus.NetworkUnavailable, result.Status);
            Assert.Equal(VelopackAppUpdater.NetworkUnavailableMessage, result.Message);
        }

        [Fact]
        public void HandleException_ShouldReturnReadableChecksumMessage()
        {
            AppUpdateOperationResult result = VelopackAppUpdater.HandleException(
                new ChecksumFailedException("WattPilot-2.1.0-full.nupkg"),
                "téléchargement de mise à jour");

            Assert.False(result.Success);
            Assert.Equal(AppUpdateStatus.ChecksumFailed, result.Status);
            Assert.Equal(VelopackAppUpdater.ChecksumFailedMessage, result.Message);
        }
    }
}
