"use client";

import { useState, useRef, useEffect } from "react";
import styles from "./createFlashcard.module.css";
import { auth } from "../../initializeFirebase"; 
import { useRouter, useSearchParams } from "next/navigation";
import { Globe, Lock, Trash2, Plus } from "lucide-react"; 

interface Card {
  id: number;
  term: string;
  definition: string;
}

interface FlashcardCreatorProps {
  role: "student" | "teacher";
}

const DragIndicatorIcon = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" fill="currentColor" xmlns="http://www.w3.org/2000/svg" style={{ color: '#9ca3af', cursor: 'grab' }}>
    <path d="M11 18c0 1.1-.9 2-2 2s-2-.9-2-2 .9-2 2-2 2 .9 2 2zm-2-8c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0-6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm6 4c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm0 2c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0 6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z" />
  </svg>
);

export default function FlashcardCreator({ role }: FlashcardCreatorProps) {
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [isPublic, setIsPublic] = useState(false); 
  const [isReordering, setIsReordering] = useState(false); 

  const [cards, setCards] = useState<Card[]>([
    { id: 1, term: "", definition: "" },
    { id: 2, term: "", definition: "" },
  ]);

  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  // EDIT MODE LOGIC
  const searchParams = useSearchParams();
  const deckId = searchParams.get('id');
  const [isEditMode, setIsEditMode] = useState(false);

  // Drag Refs
  const dragItem = useRef<number | null>(null);
  const dragOverItem = useRef<number | null>(null);
  const [dragEnabled, setDragEnabled] = useState(false); 

  const router = useRouter();

  // --- FETCH DECK IF EDITING ---
  useEffect(() => {
    if (!deckId) return;

    const fetchDeck = async () => {
        setIsEditMode(true);
        setIsLoading(true);
        const user = auth.currentUser;
        if (!user) return;
        const idToken = await user.getIdToken();

        try {
            const res = await fetch(`http://localhost:5261/api/flashcardsets/${deckId}`, {
                headers: { 'Authorization': `Bearer ${idToken}` }
            });
            if (res.ok) {
                const data = await res.json();
                setTitle(data.title);
                setDescription(data.description || "");
                setIsPublic(data.visibility === true || String(data.visibility).toLowerCase() === "public");
            }

            const cardsRes = await fetch(`http://localhost:5261/api/flashcardsets/${deckId}/cards`, {
                headers: { 'Authorization': `Bearer ${idToken}` }
            });
            if (cardsRes.ok) {
                const cardsData = await cardsRes.json();
                const mappedCards = cardsData.map((c: any, index: number) => ({
                    id: index + 1, 
                    term: c.term,
                    definition: c.definition
                }));
                if (mappedCards.length > 0) setCards(mappedCards);
            }
        } catch (err) {
            console.error(err);
            setError("Failed to load deck for editing.");
        } finally {
            setIsLoading(false);
        }
    };

    if(auth.currentUser) fetchDeck();
    else setTimeout(fetchDeck, 500); 

  }, [deckId]);

  // --- Auto Resize Textarea ---
  const autoResize = (e: any) => {
    e.target.style.height = 'auto'; 
    e.target.style.height = e.target.scrollHeight + 'px';
  };

  const addCard = () => {
    setCards((prev) => [
      ...prev,
      { id: prev.length > 0 ? Math.max(...prev.map(c => c.id)) + 1 : 1, term: "", definition: "" },
    ]);
  };

  const updateCard = (id: number, field: "term" | "definition", value: string) => {
    setCards((prev) =>
      prev.map((card) =>
        card.id === id ? { ...card, [field]: value } : card
      )
    );
  };

  const removeCard = (id: number) => {
    setCards((prev) => prev.filter((card) => card.id !== id));
  };

  // --- Drag Functions ---
  const handleDragStart = (e: React.DragEvent<HTMLDivElement>, position: number) => {
    dragItem.current = position;
    e.dataTransfer.effectAllowed = "move";
  };

  const handleDragEnter = (e: React.DragEvent<HTMLDivElement>, position: number) => {
    dragOverItem.current = position;
    e.preventDefault(); 
  };

  const handleDragEnd = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    if (dragItem.current !== null && dragOverItem.current !== null) {
      const copyListItems = [...cards];
      const dragItemContent = copyListItems[dragItem.current];
      copyListItems.splice(dragItem.current, 1);
      copyListItems.splice(dragOverItem.current, 0, dragItemContent);
      dragItem.current = null;
      dragOverItem.current = null;
      setCards(copyListItems);
    }
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError(null);

    if (!title.trim()) {
      alert("Please add a title.");
      setIsLoading(false);
      return;
    }
    const validCards = cards.filter(c => c.term.trim() && c.definition.trim());
    if (validCards.length === 0) {
      alert("Please add at least one card with a term and definition.");
      setIsLoading(false);
      return;
    }

    const user = auth.currentUser;
    if (!user) {
      router.push('/login');
      return;
    }
    const idToken = await user.getIdToken();

    const cardData = validCards.map(card => ({
      term: card.term,
      definition: card.definition,
    }));

    // --- FIX: Include CreatedByUID and TagIds ---
    const body = JSON.stringify({
      title: title,
      description: description,
      visibility: isPublic, 
      cards: cardData,
      tagIds: [], 
      createdByRole: role,
      createdByUID: user.uid // <--- CRITICAL FIX for validation error
    });

    try {
      const url = isEditMode 
        ? `http://localhost:5261/api/flashcardsets/${deckId}`
        : 'http://localhost:5261/api/flashcardsets';
        
      const method = isEditMode ? 'PUT' : 'POST';

      const response = await fetch(url, {
        method: method,
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${idToken}`,
        },
        body: body,
      });

      if (!response.ok) {
        let errorMessage = "Failed to save deck.";
        try {
          const errorData = await response.json();
          // Extract validation errors nicely
          if (errorData.errors) {
             const validationErrors = Object.values(errorData.errors).flat().join(', ');
             errorMessage = `Validation Error: ${validationErrors}`;
          } else {
             errorMessage = errorData.message || errorData.title || errorMessage;
          }
        } catch (jsonError) {
          errorMessage = `${response.status}: ${response.statusText}`;
        }
        throw new Error(errorMessage);
      }

      if (role === "teacher") {
        router.push('/teacher-dashboard');
      } else {
        router.push('/dashboard');
      }

    } catch (err: any) {
      setError(err.message);
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className={styles.page}>
      
      {/* ERROR BANNER */}
      {error && (
        <div style={{
            maxWidth: '1280px', margin: '0 auto 20px', padding: '15px', 
            background: '#fee2e2', color: '#dc2626', borderRadius: '8px', border: '1px solid #fecaca'
        }}>
            <strong>Error:</strong> {error}
        </div>
      )}

      <form className={styles.layout} onSubmit={handleSave}>
        <section className={styles.leftColumn}>
          <div className={styles.panel}>
            <h2 className={styles.sectionTitle}>
                {isEditMode ? "Edit Deck" : (role === "teacher" ? "Create Class Deck" : "Create a new Flashcard")}
            </h2>
            <input
              className={styles.input}
              placeholder="Title"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
            />
            <textarea
              className={`${styles.input} ${styles.textarea}`}
              placeholder="Write a description..."
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
          </div>

          <div className={styles.panel}>
            <h3 className={styles.sectionSubtitle}>Manage access</h3>
            <p className={styles.muted}>
              Choose who can view this deck.
            </p>
            
            <div style={{ display: 'flex', flexDirection: 'column', gap: '10px', marginTop: '15px' }}>
              <label className={styles.radioLabel} style={{ borderColor: !isPublic ? '#666' : 'transparent', backgroundColor: !isPublic ? 'rgba(0,0,0,0.05)' : 'transparent' }}>
                <input type="radio" name="visibility" checked={!isPublic} onChange={() => setIsPublic(false)} />
                <Lock size={18} />
                <div>
                  <span style={{display: 'block', fontWeight: 600}}>Private</span>
                  <span style={{fontSize: '0.85em', color: '#666'}}>Only you can see this.</span>
                </div>
              </label>

              <label className={styles.radioLabel} style={{ borderColor: isPublic ? '#666' : 'transparent', backgroundColor: isPublic ? 'rgba(0,0,0,0.05)' : 'transparent' }}>
                <input type="radio" name="visibility" checked={isPublic} onChange={() => setIsPublic(true)} />
                <Globe size={18} />
                <div>
                  <span style={{display: 'block', fontWeight: 600}}>Public Library</span>
                  <span style={{fontSize: '0.85em', color: '#666'}}>Anyone can view this deck.</span>
                </div>
              </label>
            </div>

          </div>
        </section>

        <section className={styles.rightColumn}>
          
          <div className={styles.toolbar}>
            <button 
                type="button" 
                className={`${styles.moveBtn} ${isReordering ? styles.activeMove : ''}`}
                onClick={() => setIsReordering(!isReordering)}
            >
                Move
            </button>

            <button type="button" className={styles.addButton} onClick={addCard}>
              <Plus size={16} style={{ marginRight: '5px' }}/> Add a card
            </button>
          </div>

          <div className={styles.cardsList}>
            {cards.map((card, index) => (
              <div 
                key={card.id} 
                className={styles.card}
                draggable={isReordering && dragEnabled}
                onDragStart={(e) => handleDragStart(e, index)}
                onDragEnter={(e) => handleDragEnter(e, index)}
                onDragEnd={handleDragEnd}
                onDragOver={(e) => e.preventDefault()}
              >
                <div className={styles.cardHeader}>
                  <div className={styles.indexWrapper}>
                    <span className={styles.cardIndex}>{index + 1}</span>
                    {isReordering && (
                        <div 
                            className={styles.dragHandle}
                            onMouseEnter={() => setDragEnabled(true)}
                            onMouseLeave={() => setDragEnabled(false)}
                        >
                            <DragIndicatorIcon />
                        </div>
                    )}
                  </div>
                  
                  <div className={styles.cardIcons}>
                    {!isReordering && (
                        <button
                            type="button"
                            className={`${styles.iconBtn} ${styles.deleteBtn}`}
                            onClick={() => removeCard(card.id)}
                            aria-label="Delete card"
                        >
                            <Trash2 size={18} />
                        </button>
                    )}
                  </div>
                </div>

                <div className={styles.cardBody}>
                  <div className={styles.fieldGroup}>
                    <textarea
                      className={styles.smallInput}
                      placeholder="Enter term"
                      value={card.term}
                      onChange={(e) => {
                        updateCard(card.id, "term", e.target.value);
                        autoResize(e); 
                      }}
                      disabled={isReordering}
                      rows={1} 
                    />
                    <span className={styles.fieldLabel}>Term</span>
                  </div>
                  <div className={styles.fieldGroup}>
                    <textarea
                      className={styles.smallInput}
                      placeholder="Enter definition"
                      value={card.definition}
                      onChange={(e) => {
                        updateCard(card.id, "definition", e.target.value);
                        autoResize(e); 
                      }}
                      disabled={isReordering}
                      rows={1}
                    />
                    <span className={styles.fieldLabel}>Definition</span>
                  </div>
                </div>
              </div>
            ))}
          </div>

          <div className={styles.actionsRow}>
            <button type="button" className={styles.secondaryAction} onClick={() => router.back()}>
              Cancel
            </button>
            <button type="submit" className={styles.primaryAction} disabled={isLoading}>
              {isLoading ? "Saving..." : isEditMode ? "Update deck" : "Save deck"}
            </button>
          </div>
        </section>
      </form>
    </div>
  );
}