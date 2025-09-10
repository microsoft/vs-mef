using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Composition;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            var discovery = new AttributedPartDiscoveryV1(Resolver.DefaultInstance);
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            Console.WriteLine("Testing CreatePartsAsync with already-canceled token...");
            
            try
            {
                var result = await discovery.CreatePartsAsync(new[] { typeof(Program) }, cts.Token);
                Console.WriteLine("ERROR: No exception was thrown - the fix is not working!");
                Environment.Exit(1);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("SUCCESS: OperationCanceledException was thrown as expected!");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Wrong exception type thrown: {ex.GetType().Name}: {ex.Message}");
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex}");
            Environment.Exit(1);
        }
    }
}