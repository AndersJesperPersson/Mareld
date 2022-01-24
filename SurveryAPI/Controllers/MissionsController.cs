﻿#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SurveyAPI.Models;
using SurveyAPI;
using SurveyAPI.DTO;
using AutoMapper;
using SurveyAPI.Helpers;

namespace SurveyAPI.Controllers
{
    [Route("api/missions")]
    [ApiController] // Make sure the parameters are correct. No need to check if model is valid.
    public class MissionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MissionsController> _logger;
        private readonly IMapper _mapper;
        private readonly IFileStorageService _fileStorageService;
        private readonly string containerName = "missions";

        //using dependicy injections for db and logger.
        public MissionsController(ApplicationDbContext context, ILogger<MissionsController> logger, IMapper imapper,
            IFileStorageService fileStorageService)
        {
            _context = context;
            _logger = logger;
            _mapper = imapper;
            _fileStorageService = fileStorageService;
        }

       

        //TODO: Lägg in en begränsning på hur många objekt som hämtas åt gången. Ska inte kunna ta alla. 
        // GET: api/Missions
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MissionLandingPageDTO>>> GetMissons([FromQuery] PaginationDTO paginationDTO)
        {
            var queryable = _context.Missions.AsQueryable();
            await HttpContext.InsertParametersPaginationsInHeader(queryable);

            var mission = await queryable.Paginate(paginationDTO).ToListAsync(); // välj här hur önskar sortera responsen. Kan va nice med bokstav på missions.

            if (mission == null)
            {
                return NotFound();
            }

            return _mapper.Map<List<MissionLandingPageDTO>>(mission);


        }


        // GET: api/Missions/active
        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<Mission>>> GetActiveMissons()
        {

            return await _context.Missions.Where(x => x.IsActive == true).ToListAsync();
        }

        // GET: api/Missions/5
        [HttpGet("{id}")]
        public async Task<ActionResult<MissionDTO>> GetMission(Guid id)
        {
            var mission = await _context.Missions.Include(x=> x.MissionSurveys).ThenInclude(x=>x.Survey).ThenInclude(x => x.SurveysQuestions).ThenInclude(x => x.Question)
                .Include(x => x.MissionEmployees).ThenInclude(x => x.Employee)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (mission is null)
            {
                _logger.LogWarning($"The Id:{id} didn´t match the Id of any objects.");
                return NotFound();
            }

            var test = _mapper.Map<MissionDTO>(mission);

            return test;
        }

        // PUT: api/Missions/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutMission(Guid id, [FromForm] MissionCreationDTO missionCreationDTO )
        {
            var mission = await _context.Missions.FirstOrDefaultAsync(x => x.Id == id);
            if(mission is null)
            {
                return NotFound();
            }

            mission = _mapper.Map(missionCreationDTO, mission);
            if(missionCreationDTO.Image is not null)
            {
                mission.Image = await _fileStorageService.EditFile(containerName, 
                                missionCreationDTO.Image, mission.Image);
            }

            await _context.SaveChangesAsync();

            return Ok();


        }

        // TODO: KAN BEHÖVAS EN BINDER HÄR PGA FORM FORMATET, INTE MINST NÄR VI SKA TA IN EN LISTA AV SURVEYS MED! 
        // POST: api/Missions
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Mission>> PostMission([FromForm]MissionCreationDTO missionCreationDTO)
        {
          
            var mission = _mapper.Map<Mission>(missionCreationDTO);
            //TODO: lägg till en default bild här om använd
            if (missionCreationDTO.Image != null)
            {
                mission.Image = await _fileStorageService.SaveFile(containerName, missionCreationDTO.Image);
            }

            mission.StartDate = DateTime.Now;

            _context.Missions.Add(mission);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // DELETE: api/Missions/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMission(Guid id)
        {
            var mission = await _context.Missions.FindAsync(id);
            if (mission == null)
            {
                _logger.LogWarning($"The Id:{id} didn´t match the Id of any objects.");
                return NotFound();
            }

            _context.Missions.Remove(mission);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool MissionExists(Guid id)
        {
            return _context.Missions.Any(e => e.Id == id);
        }
    }
}
