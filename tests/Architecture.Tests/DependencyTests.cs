using System.Reflection;
using Catalog.Domain.Products;
using Inventory.Domain.Stock;
using Ordering.Domain.Orders;
using Payment.Domain.Payments;
using Shipping.Domain.Shipments;

namespace Architecture.Tests;

/// <summary>Protects Clean Architecture and microservice dependency boundaries.</summary>
public sealed class DependencyTests
{
    /// <summary>Verifies that service domains depend only on system and domain building-block assemblies.</summary>
    [Fact]
    public void DomainAssemblies_DoNotReferenceFrameworkOrOtherServiceLayers()
    {
        Assembly[] domains =
        [
            typeof(Product).Assembly,
            typeof(StockItem).Assembly,
            typeof(Order).Assembly,
            typeof(PaymentRecord).Assembly,
            typeof(Shipment).Assembly
        ];
        string[] forbiddenFragments = ["Infrastructure", "Application", "Contracts", "AspNetCore", "EntityFrameworkCore", "RabbitMQ", "Grpc"];

        foreach (Assembly domain in domains)
        {
            string[] references = domain.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty).ToArray();
            foreach (string forbidden in forbiddenFragments)
            {
                Assert.DoesNotContain(references, reference => reference.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
            }

            Assert.DoesNotContain(references, reference =>
                domains.Any(other => other != domain && string.Equals(other.GetName().Name, reference, StringComparison.Ordinal)));
        }
    }

    /// <summary>Verifies that the edge gateway has no compile-time service contract or domain dependencies.</summary>
    [Fact]
    public void Gateway_DoesNotReferenceServiceAssemblies()
    {
        Assembly gateway = Assembly.Load("Commerce.Gateway");
        string[] references = gateway.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty).ToArray();

        Assert.DoesNotContain(references, reference =>
            reference.StartsWith("Catalog.", StringComparison.Ordinal) ||
            reference.StartsWith("Cart.", StringComparison.Ordinal) ||
            reference.StartsWith("Inventory.", StringComparison.Ordinal) ||
            reference.StartsWith("Ordering.", StringComparison.Ordinal) ||
            reference.StartsWith("Payment.", StringComparison.Ordinal) ||
            reference.StartsWith("Shipping.", StringComparison.Ordinal));
    }
}
