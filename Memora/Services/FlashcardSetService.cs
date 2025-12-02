using Google.Cloud.Firestore;
using Memora.Models;
using Memora.DTOs;

namespace Memora.Services
{
    public class FlashcardSetService : IFlashcardSetService
    {
        private readonly FirestoreDb _db;
        private readonly CollectionReference _setsCollection;

        // Get the DB via Dependency Injection
        public FlashcardSetService(FirestoreDb db)
        {
            _db = db;
            // This defines the collection name once. 
            // We will reuse _setsCollection everywhere to avoid typos.
            _setsCollection = _db.Collection("flashcardSets");
        }

        public async Task<FlashcardSet> CreateSetAsync(string userId, CreateFlashcardSetRequest request)
        {
            DocumentReference docRef = _setsCollection.Document();

            var newSet = new FlashcardSet
            {
                SetId = docRef.Id,
                UserId = userId,
                CreatedBy = await GetUsernameAsync(userId),
                Title = request.Title,
                Description = request.Description,
                Visibility = request.Visibility,
                DateCreated = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                TagIds = request.TagIds ?? new List<string>()
            };

            // 1. Save the main FlashcardSet document
            await docRef.SetAsync(newSet);
            
            // 2. Add logic to save the individual flashcards to a subcollection
            CollectionReference cardsCollection = docRef.Collection("cards");
            
            // We can use a WriteBatch for efficiency and atomicity (all or nothing)
            WriteBatch batch = _db.StartBatch(); 

            foreach (var cardDto in request.Cards)
            {
                DocumentReference cardDocRef = cardsCollection.Document();
                var newCard = new Flashcard
                {
                    CardId = cardDocRef.Id,
                    Term = cardDto.Term,
                    Definition = cardDto.Definition,
                    ImageUrl = cardDto.ImageUrl,
                    DateCreated = DateTime.UtcNow
                };
                
                // Add the flashcard to the batch
                batch.Set(cardDocRef, newCard); 
            }
            
            // Commit all card saves together
            await batch.CommitAsync();
            
            // The main set and all cards are now saved.
            return newSet;
        }

        public async Task<IEnumerable<FlashcardSet>> GetSetsForUserAsync(string userId)
        {
            Query query = _setsCollection.WhereEqualTo("user_id", userId);
            QuerySnapshot snapshot = await query.GetSnapshotAsync();

            List<FlashcardSet> sets = new List<FlashcardSet>();
            foreach (DocumentSnapshot document in snapshot.Documents)
            {
                sets.Add(document.ConvertTo<FlashcardSet>());
            }

            return sets;
        }

        public async Task<string> GetUsernameAsync(string userId)
        {
            var userRef = _db.Collection("users").Document(userId);
            var userDoc = await userRef.GetSnapshotAsync();

            if (userDoc.Exists && userDoc.ContainsField("username"))
            {
                return userDoc.GetValue<string>("username");
            }

            return "Unknown";
        }

        public async Task<List<FlashcardSet>> GetPublicSetsAsync()
        {
            // 1. Use _setsCollection (it is already initialized to "flashcardSets")
            // 2. Query where visibility is true
            Query query = _setsCollection.WhereEqualTo("visibility", true);
            
            QuerySnapshot snapshot = await query.GetSnapshotAsync();
            
            var sets = new List<FlashcardSet>();
            
            foreach (DocumentSnapshot document in snapshot.Documents)
            {
                if (document.Exists)
                {
                    var set = document.ConvertTo<FlashcardSet>();
                    set.SetId = document.Id; 
                    sets.Add(set);
                }
            }
            
            return sets;
        }

        public async Task<bool> DeleteSetAsync(string setId, string userId)
        {
            DocumentReference setDocRef = _setsCollection.Document(setId);
            DocumentSnapshot snapshot = await setDocRef.GetSnapshotAsync();

            if (!snapshot.Exists) return false;

            // Check ownership
            FlashcardSet set = snapshot.ConvertTo<FlashcardSet>();
            if (set.UserId != userId) 
            {
                // User tries to delete someone else's deck -> Fail
                return false; 
            }

            // 1. We must delete all flashcards in the subcollection first
            CollectionReference cardsCollection = setDocRef.Collection("cards");
            QuerySnapshot cardsSnapshot = await cardsCollection.GetSnapshotAsync();

            WriteBatch batch = _db.StartBatch();

            foreach (DocumentSnapshot cardDoc in cardsSnapshot.Documents)
            {
                batch.Delete(cardDoc.Reference);
            }

            // 2. Delete the main set document
            batch.Delete(setDocRef);

            // 3. Commit the batch delete
            await batch.CommitAsync();

            return true;
        }

        public async Task<FlashcardSet?> GetSetByIdAsync(string setId)
        {
            DocumentSnapshot snapshot = await _setsCollection.Document(setId).GetSnapshotAsync();
            if (!snapshot.Exists) return null;

            var set = snapshot.ConvertTo<FlashcardSet>();
            set.SetId = snapshot.Id;

            // Load cards subcollection
            var cardsSnapshot = await _setsCollection
                .Document(setId)
                .Collection("cards")
                .GetSnapshotAsync();

            set.Cards = cardsSnapshot.Documents.Select(doc =>
            {
                var card = doc.ConvertTo<Flashcard>();
                card.CardId = doc.Id;
                return card;
            }).ToList();

            return set;
        }

        public async Task<List<Flashcard>> GetCardsForSetAsync(string setId)
        {
            var cardsSnapshot = await _setsCollection
                .Document(setId)
                .Collection("cards")
                .GetSnapshotAsync();

            var cards = cardsSnapshot.Documents.Select(doc =>
            {
                var card = doc.ConvertTo<Flashcard>();
                card.CardId = doc.Id;
                return card;
            }).ToList();

            return cards;
        }

        public async Task<bool> UpdateSetAsync(string setId, string userId, CreateFlashcardSetRequest request)
        {
            DocumentReference setDocRef = _setsCollection.Document(setId);
            DocumentSnapshot snapshot = await setDocRef.GetSnapshotAsync();

            if (!snapshot.Exists) return false;

            FlashcardSet set = snapshot.ConvertTo<FlashcardSet>();
            if (set.UserId != userId) return false;

            var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc); // Safe UTC

            var updates = new Dictionary<string, object?>
            {
                { "title", request.Title },
                { "description", request.Description },
                { "visibility", request.Visibility },
                { "last_updated", now },
                { "tag_ids", request.TagIds ?? new List<string>() }
            };
            await setDocRef.UpdateAsync(updates);

            CollectionReference cardsCollection = setDocRef.Collection("cards");
            QuerySnapshot existingCards = await cardsCollection.GetSnapshotAsync();
            WriteBatch batch = _db.StartBatch();
            
            // Delete old cards
            foreach (DocumentSnapshot cardDoc in existingCards.Documents)
            {
                batch.Delete(cardDoc.Reference);
            }
            
            // Add new cards (even if list is larger/smaller than before)
            if (request.Cards != null)
            {
                foreach (var cardDto in request.Cards)
                {
                    DocumentReference newCardRef = cardsCollection.Document();
                    var newCard = new Flashcard
                    {
                        CardId = newCardRef.Id,
                        Term = cardDto.Term,
                        Definition = cardDto.Definition,
                        ImageUrl = cardDto.ImageUrl,
                        DateCreated = now
                    };
                    batch.Set(newCardRef, newCard);
                }
            }

            await batch.CommitAsync();
            return true;
        }
    }
}