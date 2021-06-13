﻿// Copyright (c) 2021 Quetzal Rivera.
// Licensed under the GNU General Public License v3.0, See LICENCE in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using SauceNao.Data;
using System.IO;
using System.Threading.Tasks;

namespace SauceNao.Controllers
{
    /// <summary>This class allows you to recover temporary files generated by the SauceNao bot.</summary>
    [Route("temp")]
    [ApiController]
    public class TemporalFilesController : ControllerBase
    {
        private readonly SauceNaoContext _context;
        /// <summary>Initialize a new instance of TemporalFilesController</summary>
        /// <param name="context">The database session for queries</param>
        public TemporalFilesController(SauceNaoContext context)
        {
            _context = context;
        }

        // GET: api/temp/5
        ///<Summary>If file exists, return file.</Summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTemporalFile(string id)
        {
            var fileUniqueId = id;
            var tempFile = await _context.TemporalFiles.AsNoTracking()
                .FirstOrDefaultAsync(f => f.FileUniqueId == fileUniqueId);
            if (tempFile == default)
            {
                return NotFound();
            }
            string tmp = Path.GetTempPath();
            string path = $"{tmp}{tempFile.FilePath}";
            // If file not exist return NotFound!
            if (!System.IO.File.Exists(path))
            {
                _context.Remove(tempFile);
                await _context.SaveChangesAsync().ConfigureAwait(false);
                return NotFound();
            }

            byte[] filearray = await System.IO.File.ReadAllBytesAsync(path).ConfigureAwait(false);
            new FileExtensionContentTypeProvider().TryGetContentType(tempFile.FilePath, out string contentType);
            return File(filearray, contentType ?? "application/octet-stream", tempFile.FilePath);
        }
    }
}