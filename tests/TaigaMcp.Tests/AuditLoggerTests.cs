using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using TaigaMcp.Configuration;
using TaigaMcp.Logging;
using TaigaMcp.Tests.Support;
using Xunit;

namespace TaigaMcp.Tests;

public class AuditLoggerTests
{
    [Fact]
    public void Record_WritesSingleJsonLineToFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"audit-{Guid.NewGuid():N}.log");
        var options = new AuditOptions { Enabled = true, FilePath = path };
        var logger = new AuditLogger(NullLogger<AuditLogger>.Instance, new HttpContextAccessor(), options);

        try
        {
            logger.Record("taiga_delete_project", new { project_id = 7 }, success: true, error: null);

            var line = Assert.Single(File.ReadAllLines(path));
            Assert.Contains("taiga_delete_project", line);
            Assert.Contains("project_id", line);
            Assert.Contains("true", line);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Record_WhenDisabled_DoesNotWrite()
    {
        var path = Path.Combine(Path.GetTempPath(), $"audit-{Guid.NewGuid():N}.log");
        var options = new AuditOptions { Enabled = false, FilePath = path };
        var logger = new AuditLogger(NullLogger<AuditLogger>.Instance, new HttpContextAccessor(), options);

        logger.Record("taiga_delete_project", new { project_id = 7 }, success: true, error: null);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Record_WritesToLoggerWithToolAndError()
    {
        var capturing = new CapturingLogger<AuditLogger>();
        var options = new AuditOptions { Enabled = true, FilePath = null };
        var logger = new AuditLogger(capturing, new HttpContextAccessor(), options);

        logger.Record("taiga_delete_task", new { task_id = 3 }, success: false, error: "boom");

        Assert.Contains(capturing.Messages, m =>
            m.Contains("taiga_delete_task", StringComparison.Ordinal) &&
            m.Contains("boom", StringComparison.Ordinal));
    }

    [Fact]
    public void Record_AppendsMultipleJsonLinesAndPreservesJsonNodeArguments()
    {
        var path = Path.Combine(Path.GetTempPath(), $"audit-{Guid.NewGuid():N}.log");
        var options = new AuditOptions { Enabled = true, FilePath = path };
        var logger = new AuditLogger(NullLogger<AuditLogger>.Instance, new HttpContextAccessor(), options);

        try
        {
            var arguments = JsonNode.Parse("""{"epic_id":42}""");

            logger.Record("taiga_delete_epic", arguments, success: true, error: null);
            logger.Record("taiga_delete_epic", arguments, success: false, error: "fail");

            var lines = File.ReadAllLines(path);
            Assert.Equal(2, lines.Length);
            Assert.Contains("\"epic_id\":42", lines[0]);
            Assert.Contains("\"Success\":true", lines[0]);
            Assert.Contains("fail", lines[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Record_IncludesClientIpFromHttpContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"audit-{Guid.NewGuid():N}.log");
        var options = new AuditOptions { Enabled = true, FilePath = path };
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
        var accessor = new HttpContextAccessor { HttpContext = context };
        var logger = new AuditLogger(NullLogger<AuditLogger>.Instance, accessor, options);

        try
        {
            logger.Record("taiga_delete_project", new { project_id = 1 }, success: true, error: null);

            var line = Assert.Single(File.ReadAllLines(path));
            Assert.Contains("203.0.113.10", line);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Record_WhenFileWriteFails_DoesNotThrow()
    {
        // Путь указывает на существующий каталог — запись в файл завершится ошибкой,
        // которая должна быть проглочена и залогирована как предупреждение.
        var capturing = new CapturingLogger<AuditLogger>();
        var options = new AuditOptions { Enabled = true, FilePath = Path.GetTempPath() };
        var logger = new AuditLogger(capturing, new HttpContextAccessor(), options);

        var exception = Record.Exception(
            () => logger.Record("taiga_delete_project", new { project_id = 1 }, success: true, error: null));

        Assert.Null(exception);
    }

    [Fact]
    public void Record_RotatesFileWhenSizeLimitReached()
    {
        var path = Path.Combine(Path.GetTempPath(), $"audit-{Guid.NewGuid():N}.log");
        File.WriteAllText(path, new string('x', 1_100_000)); // > 1 МБ
        var options = new AuditOptions
        {
            Enabled = true,
            FilePath = path,
            MaxFileSizeMB = 1,
            MaxBackupFiles = 2,
        };
        var logger = new AuditLogger(NullLogger<AuditLogger>.Instance, new HttpContextAccessor(), options);

        try
        {
            logger.Record("taiga_delete_project", new { project_id = 1 }, success: true, error: null);

            Assert.True(File.Exists(path + ".1"));
            var line = Assert.Single(File.ReadAllLines(path));
            Assert.Contains("taiga_delete_project", line);
        }
        finally
        {
            File.Delete(path);
            if (File.Exists(path + ".1"))
            {
                File.Delete(path + ".1");
            }
        }
    }

    [Fact]
    public void Record_Rotation_KeepsConfiguredBackupCount()
    {
        var path = Path.Combine(Path.GetTempPath(), $"audit-{Guid.NewGuid():N}.log");
        File.WriteAllText(path, new string('x', 1_100_000));
        File.WriteAllText(path + ".1", "backup-1");
        File.WriteAllText(path + ".2", "backup-2");
        var options = new AuditOptions
        {
            Enabled = true,
            FilePath = path,
            MaxFileSizeMB = 1,
            MaxBackupFiles = 2,
        };
        var logger = new AuditLogger(NullLogger<AuditLogger>.Instance, new HttpContextAccessor(), options);

        try
        {
            logger.Record("taiga_delete_project", new { project_id = 1 }, success: true, error: null);

            // Самый старый (.2) удалён, старый .1 сдвинут в .2, исходный файл стал .1.
            Assert.Equal("backup-1", File.ReadAllText(path + ".2"));
            Assert.True(new FileInfo(path + ".1").Length > 1_000_000);
        }
        finally
        {
            foreach (var file in new[] { path, path + ".1", path + ".2" })
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }
    }

    [Fact]
    public void Record_BelowSizeLimit_DoesNotRotate()
    {
        var path = Path.Combine(Path.GetTempPath(), $"audit-{Guid.NewGuid():N}.log");
        File.WriteAllText(path, "existing" + Environment.NewLine);
        var options = new AuditOptions
        {
            Enabled = true,
            FilePath = path,
            MaxFileSizeMB = 64,
            MaxBackupFiles = 5,
        };
        var logger = new AuditLogger(NullLogger<AuditLogger>.Instance, new HttpContextAccessor(), options);

        try
        {
            logger.Record("taiga_delete_project", new { project_id = 1 }, success: true, error: null);

            Assert.False(File.Exists(path + ".1"));
            Assert.Equal(2, File.ReadAllLines(path).Length);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
