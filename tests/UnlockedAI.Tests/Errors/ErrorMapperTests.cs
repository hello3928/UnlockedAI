using UnlockedAI.Core.Errors;

namespace UnlockedAI.Tests.Errors;

public class ErrorMapperTests
{
    [Fact]
    public void AppException_keeps_its_own_error()
    {
        var error = AppError.ModelNotFound("llama");

        Assert.Same(error, ErrorMapper.Map(new AppException(error)));
    }

    [Fact]
    public void Cancellation_maps_to_cancelled()
    {
        Assert.Equal(AppErrorKind.Cancelled, ErrorMapper.Map(new OperationCanceledException()).Kind);
    }

    [Fact]
    public void HttpClient_timeout_maps_to_timeout_not_cancelled()
    {
        var exception = new TaskCanceledException("timed out", new TimeoutException());

        Assert.Equal(AppErrorKind.Timeout, ErrorMapper.Map(exception).Kind);
    }

    [Fact]
    public void Http_failure_is_a_retryable_network_error()
    {
        var error = ErrorMapper.Map(new HttpRequestException("connection refused"));

        Assert.Equal(AppErrorKind.Network, error.Kind);
        Assert.True(error.IsRetryable);
        Assert.Equal("connection refused", error.Detail);
    }

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException), AppErrorKind.AccessDenied)]
    [InlineData(typeof(FileNotFoundException), AppErrorKind.FileNotFound)]
    [InlineData(typeof(DirectoryNotFoundException), AppErrorKind.FileNotFound)]
    [InlineData(typeof(IOException), AppErrorKind.FileUnreadable)]
    [InlineData(typeof(InvalidOperationException), AppErrorKind.Unknown)]
    public void File_and_unknown_exceptions_map_by_type(Type exceptionType, AppErrorKind expected)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        Assert.Equal(expected, ErrorMapper.Map(exception).Kind);
    }
}
