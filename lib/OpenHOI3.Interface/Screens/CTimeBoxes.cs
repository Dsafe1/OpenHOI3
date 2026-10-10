namespace OpenHOI3.Interface.Screens;

/// <summary>Which of the air orders window's three boxes of the hours a mission flies are ticked.</summary>
public readonly record struct CTimeBoxes(bool Day, bool Night, bool Both);
