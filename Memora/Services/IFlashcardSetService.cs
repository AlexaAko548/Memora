using Memora.Models;
using Memora.DTOs;

namespace Memora.Services
{
    public interface IFlashcardSetService
    {
        // Create a new flashcard set
        Task<FlashcardSet> CreateSetAsync(string userId, CreateFlashcardSetRequest request);

        // Get all sets belonging to a user
        Task<IEnumerable<FlashcardSet>> GetSetsForUserAsync(string userId);

        // Get all public sets
        Task<List<FlashcardSet>> GetPublicSetsAsync();

        // Delete a set
        Task<bool> DeleteSetAsync(string setId, string userId);

        // Get set by ID
        Task<FlashcardSet?> GetSetByIdAsync(string setId);

        // Get username
        Task<string> GetUsernameAsync(string userId);

        // Get cards
        Task<List<Flashcard>> GetCardsForSetAsync(string setId);

        // Update a flashcard set
        Task<bool> UpdateSetAsync(string setId, string userId, CreateFlashcardSetRequest request);
        
    }
}
