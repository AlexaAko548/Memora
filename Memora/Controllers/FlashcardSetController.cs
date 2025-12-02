using Microsoft.AspNetCore.Mvc;
using FirebaseAdmin.Auth;
using Memora.Services;
using Memora.DTOs;

namespace Memora.Controllers
{
    [ApiController]
    [Route("api/flashcardsets")] // Base URL: /api/flashcardsets
    public class FlashcardSetsController : ControllerBase
    {
        private readonly IFlashcardSetService _setService;
        private readonly FirebaseAuth _auth;

        public FlashcardSetsController(IFlashcardSetService setService)
        {
            _setService = setService;
            _auth = FirebaseAuth.DefaultInstance;
        }

        // --- Helper: Gets the authenticated user's UID from their token ---
        private async Task<string?> GetUserIdFromTokenAsync()
        {
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                var headerStr = authHeader.ToString();
                if (string.IsNullOrEmpty(headerStr) || !headerStr.StartsWith("Bearer "))
                    return null;

                string idToken = headerStr.Split(" ")[1];
                try
                {
                    FirebaseToken decodedToken = await _auth.VerifyIdTokenAsync(idToken);
                    return decodedToken.Uid;
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        // ==========================================
        // 1. NEW ENDPOINT: Get All Public Sets
        // ==========================================
        [HttpGet] // Matches GET /api/flashcardsets
        public async Task<IActionResult> GetPublicSets()
        {
            try
            {
                // NOTE: You must define 'GetPublicSetsAsync' in your IFlashcardSetService first!
                var sets = await _setService.GetPublicSetsAsync();
                return Ok(sets);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error.", error = ex.Message });
            }
        }

        [HttpPost] // POST /api/flashcardsets
        public async Task<IActionResult> CreateSet([FromBody] CreateFlashcardSetRequest request)
        {
            try
            {
                string? userId = await GetUserIdFromTokenAsync();
                if (userId == null)
                {
                    return Unauthorized(new { message = "Invalid or missing token." });
                }

                string? username = await GetUsernameFromTokenAsync();
                if (username == null)
                {
                    return Unauthorized(new { message = "User must have a username claim." });
                }

                var newSet = await _setService.CreateSetAsync(userId, request);

                return CreatedAtAction(nameof(GetSet), new { setId = newSet.SetId }, newSet);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error.", error = ex.Message });
            }
        }

        private async Task<string?> GetUsernameFromTokenAsync()
        {
            var token = HttpContext.Request.Headers["Authorization"].ToString()?.Replace("Bearer ", "");
            if (string.IsNullOrEmpty(token))
                return null;

            var decoded = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(token);
            var claims = decoded.Claims;

            if (claims.ContainsKey("username"))
                return claims["username"]?.ToString();

            if (claims.ContainsKey("name"))
                return claims["name"]?.ToString();

            if (claims.ContainsKey("email"))
                return claims["email"]?.ToString(); // fallback

            return null;
        }


        [HttpGet("my-sets")] // GET /api/flashcardsets/my-sets
        public async Task<IActionResult> GetMySets()
        {
            try
            {
                string? userId = await GetUserIdFromTokenAsync();
                if (userId == null)
                {
                    return Unauthorized(new { message = "Invalid or missing token." });
                }

                var sets = await _setService.GetSetsForUserAsync(userId);
                return Ok(sets);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error.", error = ex.Message });
            }
        }
        
        [HttpGet("{setId}")]
        public async Task<IActionResult> GetSet(string setId)
        {
            var set = await _setService.GetSetByIdAsync(setId);

            if (set == null)
                return NotFound(new { message = "Flashcard set not found." });

            return Ok(set);
        }

        [HttpDelete("{setId}")]
        public async Task<IActionResult> DeleteSet(string setId)
        {
            try
            {
                string? userId = await GetUserIdFromTokenAsync();
                if (userId == null) return Unauthorized(new { message = "Invalid or missing token." });

                bool success = await _setService.DeleteSetAsync(setId, userId);

                if (!success)
                {
                    // Either not found or not authorized (we treat them same for security)
                    return NotFound(new { message = "Set not found or you do not have permission to delete it." });
                }

                return Ok(new { message = "Flashcard set deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error.", error = ex.Message });
            }
        }

        [HttpGet("{setId}/cards")]
        public async Task<IActionResult> GetCards(string setId)
        {
            try
            {
                var set = await _setService.GetSetByIdAsync(setId);
                if (set == null)
                    return NotFound(new { message = "Set not found." });

                var cards = await _setService.GetCardsForSetAsync(setId);
                return Ok(cards);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to load cards.", error = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSet(string id, [FromBody] CreateFlashcardSetRequest request)
        {
            try
            {
                string? userId = await GetUserIdFromTokenAsync();
                if (userId == null) return Unauthorized();

                bool success = await _setService.UpdateSetAsync(id, userId, request);
                if (!success) return NotFound("Set not found or you don't have permission.");

                return Ok(new { message = "Set updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}