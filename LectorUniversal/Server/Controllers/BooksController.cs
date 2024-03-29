﻿using AutoMapper;
using LectorUniversal.Server.Data;
using LectorUniversal.Server.Helpers;
using LectorUniversal.Server.Models;
using LectorUniversal.Shared;
using LectorUniversal.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LectorUniversal.Server.Controllers
{
    //[Authorize(Roles = "admin")]
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class BooksController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileUpload _fileUpload;
        private readonly IMapper _mapper;
        private readonly UserManager<ApplicationUser> _userManager;

        public BooksController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IFileUpload fileUpload,
            IMapper mapper)
        {
            _db = db;
            _fileUpload = fileUpload;
            _mapper = mapper;
            _userManager = userManager;
        }


        [HttpGet("home")]
        [AllowAnonymous]
        public async Task<ActionResult<List<Book>>> GetNewest()
        {
            //Get 14 books
            var books = await _db.Books.Include(x => x.Chapters).OrderByDescending(x => x.CreatedOn).Take(14).ToListAsync();
            //Order previous books by date desc to get the newest book with chapter
            var results = books.Where(x => x.Chapters.Count() > 0).OrderByDescending(c => c.Chapters.Max(x => x.CreatedOn)).ToList();

            return results;
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<VisualiseBookDTO>> Get(int id)
        {
            var Editorial = await _db.Editorials.FirstOrDefaultAsync();

            var Book = await _db.Books.Where(x => x.Id == id)
                .Include(x => x.Genders).ThenInclude(x => x.Gender)
                .Include(x => x.Editorial).Where(x => x.EditorialId == Editorial.Id)
                .Include(x => x.Chapters.Where(c => c.BooksId == id))
                .FirstOrDefaultAsync();

            

            if (Book == null) { return NotFound(); } 

            var averageVote = 0.0;
            var userVote = 0;
            var userState = "";

            if (await _db.BookVotes.AnyAsync(x => x.BookId == id))
            {
                //Get the average votes from a comic
                averageVote = await _db.BookVotes.Where(x => x.BookId == id).AverageAsync(x => x.Vote);

                //Get the vote from user if the user is Authenticated
                if (HttpContext.User.Identity.IsAuthenticated)
                {
                    var user = await _userManager.FindByNameAsync(HttpContext.User.Identity.Name);
                    var userId = user.Id;

                    var userDBVote = await _db.BookVotes.FirstOrDefaultAsync(x => x.BookId == id && x.UserId == userId);

                    if (userDBVote != null)
                    {
                        userVote = userDBVote.Vote;
                    }
                }
            }

            if (await _db.BookFollows.AnyAsync(x => x.BookId == id))
            {
                if (HttpContext.User.Identity.IsAuthenticated)
                {
                    var user = await _userManager.FindByNameAsync(HttpContext.User.Identity.Name);
                    var userId = user.Id;

                    var userDBState = await _db.BookFollows.FirstOrDefaultAsync(x => x.BookId == id && x.UserId == userId);

                    if (userDBState != null)
                    {
                       userState = userDBState.BookState;
                    }
                }
            }

            var model = new VisualiseBookDTO();
            model.Book = Book;
            model.Genders = Book.Genders.Select(x => x.Gender).ToList();
            model.Chapters = Book.Chapters.ToList();
            model.AverageVote = averageVote;
            model.UserVote = userVote;
            model.BookState = userState;

            return model;
        }

        
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<List<Book>>> GetAll([FromQuery]PaginationDTO pagination)
        {
            var queryable = _db.Books.AsQueryable();
            await HttpContext.InsertParameterInResponse(queryable, pagination.Records);
            return await queryable.Pagination(pagination).ToListAsync();
        }

        [HttpGet("update/{id}")]
        public async Task<ActionResult<BookUpdateDTO>> PutGet(int id)
        {
            var bookActionResult = await Get(id);
            if (bookActionResult.Result is NotFoundResult) { return NotFound(); }

            var bookViewDTO = bookActionResult.Value;
            var gendersSelectedId = bookViewDTO.Genders.Select(x => x.Id).ToList();
            var genedersNotSelected = await _db.Genders.Where(x => !gendersSelectedId.Contains(x.Id)).ToListAsync();

            var model = new BookUpdateDTO();
            model.Book = bookViewDTO.Book;
            model.GendersNotSelected = genedersNotSelected;
            model.GendersSelected = bookViewDTO.Genders;
            return model;
        }

        [HttpPost]
        public async Task<ActionResult<int>> Post([FromBody] Book book)
        {
            //Create directory path to save the images
            if (!string.IsNullOrWhiteSpace(book.Cover))
            {
                string folder = $"{book.Name.Replace(" ", "-").Replace(":", "").Replace("#", "")}";
                var coverPoster = Convert.FromBase64String(book.Cover);
                var bookType = Enum.GetName(book.TypeofBook);
                book.Cover = await _fileUpload.SaveFile(coverPoster, "jpg",bookType, folder);
            }

            _db.Add(book);
            await _db.SaveChangesAsync();
            return book.Id;
        }

        [HttpPut]
        public async Task<ActionResult> Put(Book book)
        {
            var bookDB = await _db.Books.AsNoTracking().FirstOrDefaultAsync(x => x.Id == book.Id);

            if (bookDB == null) { return NotFound(); }

            //Edit the local path to save the images
            if (!string.IsNullOrWhiteSpace(book.Cover))
            {
                var coverImage = Convert.FromBase64String(book.Cover);
                var actualfolder = $"{bookDB.Name.Replace(" ", "-")}";
                var newfolder = $"{book.Name.Replace(" ", "-").Replace(":", "").Replace("#", "")}";
                var bookType = Enum.GetName(bookDB.TypeofBook);
                bool complete = false;
                bookDB.Cover = await _fileUpload.EditFile(coverImage, "jpg", actualfolder, newfolder, bookDB.Cover, bookType, complete);
            }

            bookDB = _mapper.Map(book, bookDB);
            
            await _db.Database.ExecuteSqlInterpolatedAsync($"delete from GenderBooks WHERE BookId = {book.Id};");

            bookDB.Genders = book.Genders;

            _db.Attach(bookDB).State = EntityState.Modified;
            await _db.GenderBooks.AddRangeAsync(bookDB.Genders);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            var exits = await _db.Books.AsNoTracking().AnyAsync(x => x.Id == id);
            if (!exits) { return NotFound(); }

            //Delete the local path completely
            var book = await _db.Books.AsNoTracking().Where(x => x.Id == id).FirstOrDefaultAsync();
            var folder = book.Name.Replace(" ", "-");
            var bookType = Enum.GetName(book.TypeofBook);
            bool complete = true;
            await _fileUpload.DeleteFile(folder,bookType, book.Cover, complete);

            _db.Remove(new Book { Id = id });
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
