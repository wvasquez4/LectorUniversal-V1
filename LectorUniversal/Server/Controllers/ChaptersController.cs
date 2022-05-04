﻿using AutoMapper;
using LectorUniversal.Server.Data;
using LectorUniversal.Server.Helpers;
using LectorUniversal.Shared;
using LectorUniversal.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LectorUniversal.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChaptersController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IMapper _mapper;
        private readonly IFileUpload _fileUpload;
        public ChaptersController(ApplicationDbContext db, IMapper mapper, IFileUpload fileUpload)
        {
            _db = db;
            _mapper = mapper;
            _fileUpload = fileUpload;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var chapters = await _db.Chapters.ToListAsync();
            var chapterDTO = _mapper.Map<ChapterDTO>(chapters);
            return Ok(chapterDTO);
        }

        [HttpPost]
        public async Task<ActionResult<int>> Post([FromBody] Chapter chapter)
        {
                    string folder = $"Comics/Flash/{chapter.Title.Replace(" ", "-")}";
            Shared.Pages pages = new Shared.Pages();
            List<string> imgUrl = new List<string>();
            foreach (var item in chapter.ChapterPages)
            {
                var ChapterPage = Convert.FromBase64String(item.ImageUrl);
                 imgUrl.Add(await _fileUpload.SaveFile(ChapterPage, "jpg", folder));
            }
            chapter.ChapterPages.RemoveRange(0,chapter.ChapterPages.Count());
            foreach (var item in imgUrl)
            {
                chapter.ChapterPages.Add(new Shared.Pages { ImageUrl = item});
            }        
            


            await _db.AddAsync(chapter);
            await _db.SaveChangesAsync();
            return Ok(chapter);
        }

    }
}
