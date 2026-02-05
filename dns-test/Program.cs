// DnsResolutionTest.cs
using System;
using System.Threading.Tasks;
using StackExchange.Redis;

class DnsResolutionTest
{
    static async Task Main(string[] args)
    {
        var hostname = args.Length > 0 ? args[0] : "your-route53-record.example.com";
        var port = args.Length > 1 ? int.Parse(args[1]) : 6379;
        var testKey = "dns-test-key-" + Guid.NewGuid().ToString("N")[..8];
        var testValue = "test-value-" + DateTime.UtcNow.Ticks;
        
        Console.WriteLine($"Testing DNS resolution with SE.Redis");
        Console.WriteLine($"Hostname: {hostname}:{port}");
        Console.WriteLine($"Test key: {testKey}");
        Console.WriteLine(new string('=', 60));

        // Step 1: Connect to Host A (current DNS target) and set a value
        Console.WriteLine("\n[STEP 1] Connect to current host and SET value");
        var config = new ConfigurationOptions
        {
            EndPoints = { { hostname, port } },
            ConnectTimeout = 5000,
            AbortOnConnectFail = false,
        };

        await using (var muxer = await ConnectionMultiplexer.ConnectAsync(config))
        {
            var db = muxer.GetDatabase();
            
            // Set the test key
            await db.StringSetAsync(testKey, testValue);
            Console.WriteLine($"  ✓ SET {testKey} = '{testValue}'");
            
            // Verify it's there
            var retrieved = await db.StringGetAsync(testKey);
            Console.WriteLine($"  ✓ GET {testKey} = '{retrieved}'");
            
            if (retrieved != testValue)
            {
                Console.WriteLine("  ✗ ERROR: Value mismatch!");
                return;
            }
            Console.WriteLine("  ✓ Value verified on Host A");
        }
        // Connection closed

        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine(">>> NOW CHANGE YOUR ROUTE53 RECORD TO POINT TO HOST B <<<");
        Console.WriteLine(">>> (Host B should be a separate Redis with empty database)");
        Console.WriteLine(">>> Press ENTER when DNS change has propagated...");
        Console.ReadLine();

        // Step 2: Connect again (should resolve to Host B) and verify key is MISSING
        Console.WriteLine("\n[STEP 2] Connect to new host (after DNS change) and GET value");
        
        await using (var muxer = await ConnectionMultiplexer.ConnectAsync(config))
        {
            var db = muxer.GetDatabase();
            
            // Try to get the test key - should be null/missing on Host B
            var retrieved = await db.StringGetAsync(testKey);
            
            Console.WriteLine($"  GET {testKey} = '{(retrieved.IsNull ? "(null)" : retrieved.ToString())}'");
            
            if (retrieved.IsNull)
            {
                Console.WriteLine("\n  ✓✓✓ SUCCESS! DNS RE-RESOLUTION CONFIRMED! ✓✓✓");
                Console.WriteLine("  The key is missing because we connected to a different host.");
            }
            else if (retrieved == testValue)
            {
                Console.WriteLine("\n  ✗✗✗ FAILURE: DNS CACHING DETECTED! ✗✗✗");
                Console.WriteLine("  The key was found, meaning we connected to the SAME host.");
                Console.WriteLine("  .NET is caching the DNS resolution.");
            }
            else
            {
                Console.WriteLine("\n  ??? UNEXPECTED: Different value found.");
                Console.WriteLine($"  Expected null or '{testValue}', got '{retrieved}'");
            }
        }

        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("Test complete!");
    }
}
