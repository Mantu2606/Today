/*using AutoMapper;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ILakshya.Dal;
using ILakshya.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ILakshya.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StudentController : ControllerBase
    {
        private readonly ICommonRepository<Student> _studentRepository;
        private readonly WebPocHubDbContext _dbContext;

        public StudentController(WebPocHubDbContext dbContext, ICommonRepository<Student> repository, IMapper mapper)
        {
            _dbContext = dbContext;
            _studentRepository = repository;
        }

        [HttpGet]
        public IEnumerable<Student> GetAll()
        {
            return _studentRepository.GetAll();
        }

        [HttpGet("{id:int}")]
        public ActionResult<Student> GetById(int id)
        {
            var student = _studentRepository.GetDetails(id);
            if (student == null)
            {
                return NotFound();
            }
            return Ok(student);
        }

        [HttpGet("ByEnrollNo/{enrollNo}")]
        public ActionResult<Student> GetStudentDetailsByEnrollNo(string enrollNo)
        {
            // Ensure enrollNo is not null or empty
            if (string.IsNullOrEmpty(enrollNo))
            {
                return BadRequest("EnrollNo cannot be null or empty.");
            }

            // Find the student by enrollNo
            var student = _studentRepository.GetAll().FirstOrDefault(s => s.EnrollNo != null && s.EnrollNo.ToString() == enrollNo);

            // If student is not found
            if (student == null)
            {
                return NotFound("Student Not found");
            }

            return Ok(student);
        }

        [HttpPost("UploadExcel")]
        public async Task<IActionResult> UploadExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            var students = new List<Student>();

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                stream.Position = 0;

                using (SpreadsheetDocument doc = SpreadsheetDocument.Open(stream, false))
                {
                    WorkbookPart workbookPart = doc.WorkbookPart;
                    Sheet sheet = workbookPart.Workbook.Sheets.Elements<Sheet>().FirstOrDefault();
                    WorksheetPart worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id);
                    SheetData sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();

                    var headers = new List<string>();
                    bool isFirstRow = true;

                    var existingStudents = _dbContext.Students.ToDictionary(s => s.EnrollNo);
                    foreach (Row row in sheetData.Elements<Row>())
                    {
                        if (isFirstRow)
                        {
                            headers = row.Elements<Cell>().Select(cell => GetCellValue(doc, cell)).ToList();
                            isFirstRow = false;
                            continue;
                        }

                        var student = new Student();
                        var cells = row.Elements<Cell>().ToArray();
                        if (cells.Length < 14) continue;

                        student.EnrollNo = cells.Length > 0 ? ParseCellValue(cells[0], doc) : null;

                        if (student.EnrollNo != null && existingStudents.TryGetValue(student.EnrollNo, out var existingStudent))
                        {
                            student = existingStudent;
                        }

                        student.Name = cells.Length > 1 ? GetCellValue(doc, cells[1]) : "Unknown";
                        student.FatherName = cells.Length > 2 ? GetCellValue(doc, cells[2]) : "Unknown";
                        student.RollNo = cells.Length > 3 ? ParseCellValue(cells[3], doc).ToString() : null;
                        student.GenKnowledge = cells.Length > 4 ? ParseCellValue(cells[4], doc) ?? 0 : 0;
                        student.Science = cells.Length > 5 ? ParseCellValue(cells[5], doc) ?? 0 : 0;
                        student.EnglishI = cells.Length > 6 ? ParseCellValue(cells[6], doc) ?? 0 : 0;
                        student.EnglishII = cells.Length > 7 ? ParseCellValue(cells[7], doc) ?? 0 : 0;
                        student.HindiI = cells.Length > 8 ? ParseCellValue(cells[8], doc) ?? 0 : 0;
                        student.HindiII = cells.Length > 9 ? ParseCellValue(cells[9], doc) ?? 0 : 0;
                        student.Computer = cells.Length > 10 ? ParseCellValue(cells[10], doc) ?? 0 : 0;
                        student.Sanskrit = cells.Length > 11 ? ParseCellValue(cells[11], doc) ?? 0 : 0;
                        student.Mathematics = cells.Length > 12 ? ParseCellValue(cells[12], doc) ?? 0 : 0;
                        student.SocialStudies = cells.Length > 13 ? ParseCellValue(cells[13], doc) ?? 0 : 0;
                        student.MaxMarks = 5;
                        student.PassMarks = 2;

                        students.Add(student);
                    }
                }
            }

            try
            {
                _dbContext.Students.AddRange(students);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }

            return Ok(students);
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<Student> Delete(int id)
        {
            var student = _studentRepository.GetDetails(id);
            if (student == null) return NotFound();

            _studentRepository.Delete(student);
            _studentRepository.SaveChanges();
            return NoContent();
        }

        [HttpDelete("ByEnrollNo/{enrollNo}")]
        public ActionResult<Student> DeleteByEnrollNo(string enrollNo)
        {
            var student = _studentRepository.GetAll().FirstOrDefault(s => s.EnrollNo?.ToString() == enrollNo);
            if (student == null)
            {
                return NotFound();
            }

            _studentRepository.Delete(student);
            _studentRepository.SaveChanges();

            return NoContent();
        }

        [HttpPut]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult Update(Student student)
        {
            _studentRepository.Update(student);
            var result = _studentRepository.SaveChanges();
            return result > 0 ? NoContent() : (ActionResult)BadRequest();
        }

        private static string GetCellValue(SpreadsheetDocument doc, Cell cell)
        {
            SharedStringTablePart sstPart = doc.WorkbookPart.SharedStringTablePart;
            string value = cell.CellValue?.InnerText;

            if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
            {
                return sstPart.SharedStringTable.ChildElements[int.Parse(value)].InnerText;
            }
            return value;
        }

        private int? ParseCellValue(Cell cell, SpreadsheetDocument doc)
        {
            string value = GetCellValue(doc, cell);
            if (value == null)
                return null;

            if (int.TryParse(value, out int parsedValue))
                return parsedValue;

            return null;
        }
    }
}*/

using AutoMapper;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ILakshya.Dal;
using ILakshya.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace ILakshya.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StudentController : ControllerBase
    {
        private readonly ICommonRepository<Student> _studentRepository;
        private readonly WebPocHubDbContext _dbContext;
        private readonly IMapper _mapper;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public StudentController(WebPocHubDbContext dbContext, ICommonRepository<Student> repository, IMapper mapper, IWebHostEnvironment webHostEnvironment)
        {
            _dbContext = dbContext;
            _studentRepository = repository;
            _mapper = mapper;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet]
        public IEnumerable<Student> GetAll()
        {
            return _studentRepository.GetAll();
        }

        [HttpGet("{id:int}")]
        public ActionResult<Student> GetById(int id)
        {
            var student = _studentRepository.GetDetails(id);
            if (student == null)
            {
                return NotFound();
            }
            return Ok(student);
        }

        [HttpGet("ByEnrollNo/{enrollNo}")]
        public ActionResult<Student> GetStudentDetailsByEnrollNo(string enrollNo)
        {
            if (string.IsNullOrEmpty(enrollNo))
            {
                return BadRequest("EnrollNo cannot be null or empty.");
            }

            var student = _studentRepository.GetAll().FirstOrDefault(s => s.EnrollNo != null && s.EnrollNo.ToString() == enrollNo);
            if (student == null)
            {
                return NotFound("Student Not found");
            }

            return Ok(student);
        }

        [HttpPost("UploadExcel")]
         public async Task<IActionResult> UploadExcel(IFormFile file)
         {
             if (file == null || file.Length == 0)
             {
                 return BadRequest("No file uploaded.");
             }

             var students = new List<Student>();

             using (var stream = new MemoryStream())
             {
                 await file.CopyToAsync(stream);
                 stream.Position = 0;

                 using (SpreadsheetDocument doc = SpreadsheetDocument.Open(stream, false))
                 {
                     WorkbookPart workbookPart = doc.WorkbookPart;
                     Sheet sheet = workbookPart.Workbook.Sheets.Elements<Sheet>().FirstOrDefault();
                     WorksheetPart worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id);
                     SheetData sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();

                     var headers = new List<string>();
                     bool isFirstRow = true;

                     var existingStudents = _dbContext.Students.ToDictionary(s => s.EnrollNo);
                     foreach (Row row in sheetData.Elements<Row>())
                     {
                         if (isFirstRow)
                         {
                             headers = row.Elements<Cell>().Select(cell => GetCellValue(doc, cell)).ToList();
                             isFirstRow = false;
                             continue;
                         }

                         var student = new Student();
                         var cells = row.Elements<Cell>().ToArray();
                         if (cells.Length < 14) continue;

                         student.EnrollNo = cells.Length > 0 ? ParseCellValue(cells[0], doc) : null;

                         if (student.EnrollNo != null && existingStudents.TryGetValue(student.EnrollNo, out var existingStudent))
                         {
                             student = existingStudent;
                         }

                         student.Name = cells.Length > 1 ? GetCellValue(doc, cells[1]) : "Unknown";
                         student.FatherName = cells.Length > 2 ? GetCellValue(doc, cells[2]) : "Unknown";
                         student.RollNo = cells.Length > 3 ? ParseCellValue(cells[3], doc).ToString() : null;
                         student.GenKnowledge = cells.Length > 4 ? ParseCellValue(cells[4], doc) ?? 0 : 0;
                         student.Science = cells.Length > 5 ? ParseCellValue(cells[5], doc) ?? 0 : 0;
                         student.EnglishI = cells.Length > 6 ? ParseCellValue(cells[6], doc) ?? 0 : 0;
                         student.EnglishII = cells.Length > 7 ? ParseCellValue(cells[7], doc) ?? 0 : 0;
                         student.HindiI = cells.Length > 8 ? ParseCellValue(cells[8], doc) ?? 0 : 0;
                         student.HindiII = cells.Length > 9 ? ParseCellValue(cells[9], doc) ?? 0 : 0;
                         student.Computer = cells.Length > 10 ? ParseCellValue(cells[10], doc) ?? 0 : 0;
                         student.Sanskrit = cells.Length > 11 ? ParseCellValue(cells[11], doc) ?? 0 : 0;
                         student.Mathematics = cells.Length > 12 ? ParseCellValue(cells[12], doc) ?? 0 : 0;
                         student.SocialStudies = cells.Length > 13 ? ParseCellValue(cells[13], doc) ?? 0 : 0;
                         student.MaxMarks = 5;
                         student.PassMarks = 2;

                         students.Add(student);
                     }
                 }
             }

             try
             {
                 _dbContext.Students.AddRange(students);
                 await _dbContext.SaveChangesAsync();
             }
             catch (Exception ex)
             {
                 return StatusCode(500, $"Internal server error: {ex.Message}");
             }

             return Ok(students);
         }

       

    
        [HttpPost("UploadProfilePicture/{id}")]
            public async Task<IActionResult> UploadProfilePicture(int id, IFormFile file)
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest("No file uploaded.");
                }

                var student = _studentRepository.GetDetails(id);
                if (student == null)
                {
                    return NotFound();
                }

                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "profile_pictures");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = $"{id}_{Path.GetRandomFileName()}{Path.GetExtension(file.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                student.ProfilePicture = $"profile_pictures/{uniqueFileName}";
                _studentRepository.Update(student);
                await _studentRepository.SaveChangesAsync();

                return Ok(new { student.ProfilePicture });
            }

            [HttpDelete("{id}")]
            [ProducesResponseType(StatusCodes.Status204NoContent)]
            [ProducesResponseType(StatusCodes.Status404NotFound)]
            public ActionResult<Student> Delete(int id)
            {
                var student = _studentRepository.GetDetails(id);
                if (student == null) return NotFound();

                _studentRepository.Delete(student);
                _studentRepository.SaveChanges();
                return NoContent();
            }

            [HttpDelete("ByEnrollNo/{enrollNo}")]
            public ActionResult<Student> DeleteByEnrollNo(string enrollNo)
            {
                var student = _studentRepository.GetAll().FirstOrDefault(s => s.EnrollNo?.ToString() == enrollNo);
                if (student == null)
                {
                    return NotFound();
                }

                _studentRepository.Delete(student);
                _studentRepository.SaveChanges();

                return NoContent();
            }

    

    private string GetCellValue(SpreadsheetDocument doc, Cell cell)
        {
            if (cell.CellValue == null) return null;

            string value = cell.CellValue.InnerText;

            if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
            {
                var stringTable = doc.WorkbookPart.SharedStringTablePart.SharedStringTable;
                value = stringTable.ElementAt(int.Parse(value)).InnerText;
            }

            return value;
        }

        private int? ParseCellValue(Cell cell, SpreadsheetDocument doc)
        {
            var value = GetCellValue(doc, cell);
            if (int.TryParse(value, out var intValue))
            {
                return intValue;
            }
            return null;
        }
    }
}

