	if (pCue->simpleWave != NULL)
	{
		FACTWave_Destroy(pCue->simpleWave);
		pCue->simpleWave = NULL;
	}
	if (pCue->playingSound != NULL)
	{
		FACT_INTERNAL_DestroySound(pCue->playingSound);
	}
