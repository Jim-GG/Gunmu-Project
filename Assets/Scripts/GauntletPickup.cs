using UnityEngine;

public class GauntletPickup : DemoInteractable
{
    [SerializeField] private CalibrationTerminal terminal;
    [SerializeField] private LabDemoFlow flow;
    public override string Prompt => terminal != null && terminal.IsSolved
        ? "[E] Equip gauntlet - combat will begin" : "Rack locked - calibrate the terminal first";

    public void Configure(CalibrationTerminal calibration, LabDemoFlow demoFlow)
    {
        terminal = calibration;
        flow = demoFlow;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (terminal == null || flow == null) return;
        if (!terminal.IsSolved)
        {
            interactor.ShowFeedback("Locked: calibrate the terminal first.");
            return;
        }
        if (flow.EquipGauntlet()) gameObject.SetActive(false);
    }
}
