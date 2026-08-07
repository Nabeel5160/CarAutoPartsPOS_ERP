using CarAutoParts.Application.DTOs.Service;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Infrastructure.Data;
using CarAutoParts.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CarAutoParts.Application.Tests;

public class KnowledgeBaseTests
{
    [Fact]
    public async Task Upsert_And_Search_Published_Only()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        await using var db = SlaPolicyTests.CreateDb(company);
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        await db.SaveChangesAsync();

        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.CurrentUser).Returns(new AppUser { Id = 1, Username = "admin" });
        var svc = new KnowledgeBaseService(
            new Repository<KbArticle>(db),
            new UnitOfWork(db),
            company,
            user.Object);

        var pub = await svc.UpsertAsync(new KbArticleUpsertDto(
            null, "Battery check", "Electrical", "Measure voltage", "battery", true));
        pub.Succeeded.Should().BeTrue();

        var draft = await svc.UpsertAsync(new KbArticleUpsertDto(
            null, "Secret draft", "Internal", "Do not publish", null, false));
        draft.Succeeded.Should().BeTrue();

        var published = await svc.ListAsync(publishedOnly: true);
        published.Should().ContainSingle(a => a.Title == "Battery check");
        published.Should().NotContain(a => a.Title == "Secret draft");

        var found = await svc.ListAsync("battery", publishedOnly: true);
        found.Should().ContainSingle();

        var del = await svc.SoftDeleteAsync(pub.Data!.Id);
        del.Succeeded.Should().BeTrue();
        (await svc.ListAsync()).Should().NotContain(a => a.Title == "Battery check");
    }
}
