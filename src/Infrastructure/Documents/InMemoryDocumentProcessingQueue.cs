using System.Threading.Channels;
using LargeDocumentProcessing.Application.Documents.Interfaces;

namespace LargeDocumentProcessing.Infrastructure.Documents;

public class InMemoryDocumentProcessingQueue : IDocumentProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(documentId, cancellationToken);
    }

    public async ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }
}
