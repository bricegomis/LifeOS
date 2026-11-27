using System.ComponentModel;
using LifeOS.Api.Assistant;
using LifeOS.Application.Health.Contracts;
using LifeOS.Domain.Health;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace LifeOS.Api.Health;

/// <summary>
/// Health tools for the assistant (ADR 0011, ADR 0018), through the same libraries as the endpoints. They
/// act with the account's rights: only the caller's own health data, never another member's. Files are
/// read by the assistant, never by the API.
/// </summary>
[McpServerToolType]
public sealed class HealthAssistantTools
{
    public const string PrivacyNote = "Données de santé privées au compte connecté (un autre membre du foyer ne les voit pas).";
    public const string UnitHelp = "Unité de la dose : gelule, comprime, g, ml, goutte ou dose.";
    public const string MomentHelp = "Moment : matin, midi, soir ou choix (peu importe, par défaut).";
    public const string PeriodHelp = "Période : year (à l'année, par défaut) ou cure ; pour une cure, cureLength et cureUnit (week ou month, " +
        "une semaine = 7 jours, un mois = 30), puis pauseLength et pauseUnit avant de recommencer (0 = une seule cure).";
    public const string DocumentTypeHelp = "Type : ordonnance, compte_rendu, resultat, bilan ou autre.";

    // Supplements

    [McpServerTool(Name = "list_supplements", ReadOnly = true, Title = "Compléments")]
    [Description("Les compléments alimentaires avec dose par jour, moment, conditionnement, liens d'achat, période (cure), coût par mois " +
        "calculé, coche du jour, série et 14 derniers jours ; total par mois. " + PrivacyNote)]
    public static async Task<SupplementListDto> ListSupplements(IHttpContextAccessor accessor, SupplementLibrary library,
        [Description("true : inclut les compléments arrêtés.")] bool includeStopped = false, CancellationToken ct = default) =>
        await library.ListAsync(await AssistantRequest.HouseholdAsync(accessor), includeStopped, ct);

    [McpServerTool(Name = "create_supplement", Title = "Créer un complément")]
    [Description("Ajoute un complément alimentaire. Le coût par mois se calcule depuis le prix du conditionnement, son contenu et la dose. " +
        UnitHelp + " " + MomentHelp + " " + PeriodHelp)]
    public static async Task<SupplementDto> CreateSupplement(IHttpContextAccessor accessor, SupplementLibrary library,
        string name,
        [Description("Dose par jour, dans l'unité.")] decimal dose,
        [Description(UnitHelp)] string unit,
        [Description("Contenu d'un conditionnement, dans la même unité que la dose.")] decimal packageContent,
        [Description("Prix payé pour un conditionnement, en euros.")] decimal? packagePrice = null,
        [Description(MomentHelp)] string? moment = null,
        [Description("Magasin ou site.")] string? shop = null,
        [Description("Liens vers les pages d'achat (https).")] List<string>? links = null,
        string? notes = null,
        [Description(PeriodHelp)] string period = "year",
        [Description("Début (aaaa-mm-jj) ; aujourd'hui par défaut.")] DateOnly? startOn = null,
        int? cureLength = null, string? cureUnit = null, int? pauseLength = null, string? pauseUnit = null,
        CancellationToken ct = default)
    {
        var request = AssistantRequest.Validated(new SupplementRequest(name, notes, dose, unit, moment, packageContent, packagePrice, shop,
            links?.Select(l => new SupplementLinkRequest(null, l)).ToList(), new SupplementPeriodRequest(period, startOn, cureLength, cureUnit, pauseLength, pauseUnit)));
        return await library.CreateAsync(await AssistantRequest.HouseholdAsync(accessor), request, ct);
    }

    [McpServerTool(Name = "update_supplement", Idempotent = true, Title = "Modifier un complément")]
    [Description("Modifie un complément ; un champ omis reste inchangé. links remplace toute la liste. " + UnitHelp + " " + MomentHelp + " " + PeriodHelp)]
    public static async Task<SupplementDto> UpdateSupplement(IHttpContextAccessor accessor, SupplementLibrary library, Guid supplementId,
        string? name = null, decimal? dose = null, string? unit = null, decimal? packageContent = null, decimal? packagePrice = null,
        string? moment = null, string? shop = null, List<string>? links = null, string? notes = null,
        string? period = null, DateOnly? startOn = null, int? cureLength = null, string? cureUnit = null, int? pauseLength = null, string? pauseUnit = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var s = (await library.GetAsync(household, supplementId, ct)).OrNotFound("Complément");
        var p = s.Period;
        var request = AssistantRequest.Validated(new SupplementRequest(name ?? s.Name, notes ?? s.Notes, dose ?? s.Dose, unit ?? s.Unit, moment ?? s.Moment,
            packageContent ?? s.PackageContent, packagePrice ?? s.PackagePrice, shop ?? s.Shop,
            links is null ? s.Links.Select(l => new SupplementLinkRequest(l.Label, l.Url)).ToList() : links.Select(l => new SupplementLinkRequest(null, l)).ToList(),
            new SupplementPeriodRequest(period ?? p.Kind, startOn ?? p.StartOn, cureLength ?? p.CureLength, cureUnit ?? p.CureUnit, pauseLength ?? p.PauseLength,
                pauseUnit ?? p.PauseUnit)));
        return (await library.UpdateAsync(household, supplementId, request, ct)).OrNotFound("Complément");
    }

    [McpServerTool(Name = "set_supplement_taken", Idempotent = true, Title = "Cocher un complément")]
    [Description("Coche (ou décoche avec taken = false) un complément pris un jour ; une coche par jour.")]
    public static async Task<SupplementDto> SetSupplementTaken(IHttpContextAccessor accessor, SupplementLibrary library, Guid supplementId,
        [Description("Jour (aaaa-mm-jj) ; aujourd'hui (Paris) par défaut, jamais dans le futur.")] DateOnly? day = null,
        bool taken = true, CancellationToken ct = default) =>
        (await library.SetTakenAsync(await AssistantRequest.HouseholdAsync(accessor), supplementId, day, taken, ct)).OrNotFound("Complément");

    [McpServerTool(Name = "stop_supplement", Idempotent = true, Title = "Arrêter un complément")]
    [Description("Arrête un complément (il garde son historique), ou le reprend avec resume = true.")]
    public static async Task<SupplementDto> StopSupplement(IHttpContextAccessor accessor, SupplementLibrary library, Guid supplementId,
        bool resume = false, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        return (resume ? await library.ResumeAsync(household, supplementId, ct) : await library.StopAsync(household, supplementId, ct)).OrNotFound("Complément");
    }

    // Symptoms

    [McpServerTool(Name = "get_symptom_journal", ReadOnly = true, Title = "Journal des symptômes")]
    [Description("Le journal des symptômes des derniers jours (jours notés seulement, « rien à signaler » compris) et la liste des " +
        "symptômes suivis (créée avec une liste proposée la première fois). " + PrivacyNote)]
    public static async Task<SymptomJournalDto> GetSymptomJournal(IHttpContextAccessor accessor, SymptomLibrary library,
        [Description("Nombre de jours, 30 par défaut.")] int days = 30, CancellationToken ct = default) =>
        await library.JournalAsync(await AssistantRequest.HouseholdAsync(accessor), days, ct);

    [McpServerTool(Name = "create_symptom_type", Title = "Suivre un nouveau symptôme")]
    [Description("Ajoute un symptôme à la liste suivie, quand get_symptom_journal ne l'a pas déjà.")]
    public static async Task<SymptomTypeDto> CreateSymptomType(IHttpContextAccessor accessor, SymptomLibrary library, string name,
        [Description("physique (par défaut) ou mental.")] string? kind = null, CancellationToken ct = default) =>
        await library.CreateTypeAsync(await AssistantRequest.HouseholdAsync(accessor), AssistantRequest.Validated(new SymptomTypeRequest(name, kind)), ct);

    [McpServerTool(Name = "log_symptom", Title = "Noter un symptôme")]
    [Description("Note un symptôme ressenti (« j'ai mal au ventre, 6/10 ») : intensité de 1 à 10, jour, heure et note facultatifs. " +
        "Retire le « rien à signaler » de ce jour.")]
    public static async Task<SymptomEntryDto> LogSymptom(IHttpContextAccessor accessor, SymptomLibrary library,
        [Description("Symptôme suivi (get_symptom_journal, ou create_symptom_type).")] Guid symptomTypeId,
        [Description("Intensité de 1 à 10.")] int intensity,
        [Description("Jour (aaaa-mm-jj) ; aujourd'hui par défaut.")] DateOnly? day = null,
        [Description("Heure (HH:mm).")] TimeOnly? time = null,
        string? note = null, CancellationToken ct = default) =>
        await library.AddEntryAsync(await AssistantRequest.HouseholdAsync(accessor),
            AssistantRequest.Validated(new SymptomEntryRequest(symptomTypeId, day, time, intensity, note)), ct);

    [McpServerTool(Name = "delete_symptom_entry", Destructive = true, Title = "Supprimer un symptôme noté")]
    [Description("Supprime un symptôme noté par erreur.")]
    public static async Task<string> DeleteSymptomEntry(IHttpContextAccessor accessor, SymptomLibrary library, Guid entryId, CancellationToken ct = default) =>
        await library.RemoveEntryAsync(await AssistantRequest.HouseholdAsync(accessor), entryId, ct) ? "Symptôme supprimé." : throw AssistantRequest.NotFound("Symptôme noté");

    [McpServerTool(Name = "save_symptom_checkin", Title = "Bilan du jour")]
    [Description("Le bilan du jour : calm = true pour « rien à signaler », sinon les symptômes ressentis avec leur intensité (1 à 10).")]
    public static async Task<SymptomDayDto> SaveSymptomCheckin(IHttpContextAccessor accessor, SymptomLibrary library,
        bool calm,
        [Description("Symptômes ressentis (symptomTypeId et intensity) quand calm = false.")] List<CheckinSymptomRequest>? symptoms = null,
        [Description("Jour (aaaa-mm-jj) ; aujourd'hui par défaut.")] DateOnly? day = null,
        CancellationToken ct = default) =>
        await library.CheckinAsync(await AssistantRequest.HouseholdAsync(accessor), AssistantRequest.Validated(new CheckinRequest(day, calm, symptoms)), ct);

    // Markers, analyses and measures

    [McpServerTool(Name = "list_markers", ReadOnly = true, Title = "Marqueurs")]
    [Description("Les marqueurs suivis (sanguins ou autres) avec unité, plage du labo, cible personnelle, fréquence (0 = une fois), bilans, " +
        "valeurs dans le temps, position de la dernière (below_lab, above_lab, in_target, normal = dans le labo hors cible, in_lab = dans le labo sans cible fixée), tendance " +
        "et prochaine mesure (due, late). " + PrivacyNote)]
    public static async Task<MarkerListDto> ListMarkers(IHttpContextAccessor accessor, AnalysisLibrary library, bool includeArchived = false,
        CancellationToken ct = default) =>
        await library.MarkersAsync(await AssistantRequest.HouseholdAsync(accessor), includeArchived, ct);

    [McpServerTool(Name = "create_marker", Title = "Créer un marqueur")]
    [Description("Ajoute un marqueur. Reprenez l'orthographe et l'unité d'un marqueur existant (list_markers) plutôt que d'en créer un doublon.")]
    public static async Task<MarkerDto> CreateMarker(IHttpContextAccessor accessor, AnalysisLibrary library,
        string name, string unit,
        [Description("Plage de référence du laboratoire.")] decimal? labMin = null, decimal? labMax = null,
        [Description("Cible personnelle (souvent plus étroite), à confirmer avec l'utilisateur.")] decimal? targetMin = null, decimal? targetMax = null,
        [Description("Mois entre deux mesures ; 0 = une fois.")] int frequencyMonths = 12,
        [Description("Bilans auxquels il appartient (ex. Bilan fer).")] List<string>? panels = null,
        [Description(DescriptionHelp)] string? description = null,
        [Description(WhenLowHelp)] string? whenLow = null,
        [Description(WhenHighHelp)] string? whenHigh = null,
        CancellationToken ct = default) =>
        await library.CreateMarkerAsync(await AssistantRequest.HouseholdAsync(accessor),
            AssistantRequest.Validated(new MarkerRequest(name, unit, labMin, labMax, targetMin, targetMax, frequencyMonths, panels, description, whenLow, whenHigh)), ct);

    private const string DescriptionHelp = "À quoi il sert : ce qu'il mesure, en langage simple (quelques phrases, 2000 caractères maximum).";
    private const string WhenLowHelp = "Si trop bas : causes fréquentes, signes possibles, ce qu'il faut savoir ou faire (informatif, pas un diagnostic).";
    private const string WhenHighHelp = "Si trop haut : causes fréquentes, signes possibles, ce qu'il faut savoir ou faire (informatif, pas un diagnostic).";

    [McpServerTool(Name = "update_marker", Idempotent = true, Title = "Modifier un marqueur")]
    [Description("Modifie un marqueur ; un champ omis reste inchangé, un texte vide (\"\") l'efface. archive = true l'archive, false le rétablit.")]
    public static async Task<MarkerDto> UpdateMarker(IHttpContextAccessor accessor, AnalysisLibrary library, Guid markerId,
        string? name = null, string? unit = null, decimal? labMin = null, decimal? labMax = null, decimal? targetMin = null, decimal? targetMax = null,
        int? frequencyMonths = null, List<string>? panels = null,
        [Description(DescriptionHelp)] string? description = null,
        [Description(WhenLowHelp)] string? whenLow = null,
        [Description(WhenHighHelp)] string? whenHigh = null,
        bool? archive = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var m = (await library.MarkerAsync(household, markerId, ct)).OrNotFound("Marqueur");
        var request = AssistantRequest.Validated(new MarkerRequest(name ?? m.Name, unit ?? m.Unit, labMin ?? m.LabMin, labMax ?? m.LabMax, targetMin ?? m.TargetMin,
            targetMax ?? m.TargetMax, frequencyMonths ?? m.FrequencyMonths, panels ?? m.Panels,
            description ?? m.Description, whenLow ?? m.WhenLow, whenHigh ?? m.WhenHigh));
        return (await library.UpdateMarkerAsync(household, markerId, request, archive, ct)).OrNotFound("Marqueur");
    }

    [McpServerTool(Name = "list_analyses", ReadOnly = true, Title = "Analyses")]
    [Description("Les analyses de laboratoire, de la plus récente à la plus ancienne, avec leurs valeurs et leur document.")]
    public static async Task<List<AnalysisDto>> ListAnalyses(IHttpContextAccessor accessor, AnalysisLibrary library, CancellationToken ct = default) =>
        await library.AnalysesAsync(await AssistantRequest.HouseholdAsync(accessor), ct);

    [McpServerTool(Name = "create_analysis", Title = "Saisir une analyse")]
    [Description("Saisit une analyse : un jour de prélèvement et une valeur par marqueur (dans l'unité du marqueur ; convertissez " +
        "seulement si le compte rendu donne l'équivalence, sinon demandez). Lue depuis un document : passez documentId, faites " +
        "d'abord confirmer les valeurs à l'utilisateur, et créez les marqueurs manquants avec create_marker.")]
    public static async Task<AnalysisDto> CreateAnalysis(IHttpContextAccessor accessor, AnalysisLibrary library,
        [Description("Valeurs : markerId et value.")] List<AnalysisValueRequest> values,
        [Description("Jour du prélèvement (aaaa-mm-jj) ; aujourd'hui par défaut.")] DateOnly? day = null,
        string? laboratory = null, string? notes = null,
        [Description("Document de santé d'où viennent les valeurs.")] Guid? documentId = null,
        CancellationToken ct = default) =>
        await library.CreateAnalysisAsync(await AssistantRequest.HouseholdAsync(accessor),
            AssistantRequest.Validated(new AnalysisRequest(day, laboratory, notes, documentId, values)), ct);

    [McpServerTool(Name = "update_analysis", Idempotent = true, Title = "Modifier une analyse")]
    [Description("Modifie une analyse ; un champ omis reste inchangé, values remplace toutes les valeurs.")]
    public static async Task<AnalysisDto> UpdateAnalysis(IHttpContextAccessor accessor, AnalysisLibrary library, Guid analysisId,
        List<AnalysisValueRequest>? values = null, DateOnly? day = null, string? laboratory = null, string? notes = null, Guid? documentId = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var a = (await library.AnalysisAsync(household, analysisId, ct)).OrNotFound("Analyse");
        var request = AssistantRequest.Validated(new AnalysisRequest(day ?? a.Day, laboratory ?? a.Laboratory, notes ?? a.Notes, documentId ?? a.DocumentId,
            values ?? a.Values.Select(v => new AnalysisValueRequest(v.MarkerId, v.Value)).ToList()));
        return (await library.UpdateAnalysisAsync(household, analysisId, request, ct)).OrNotFound("Analyse");
    }

    [McpServerTool(Name = "delete_analysis", Destructive = true, Title = "Supprimer une analyse")]
    [Description("Supprime une analyse et ses valeurs (les marqueurs restent).")]
    public static async Task<string> DeleteAnalysis(IHttpContextAccessor accessor, AnalysisLibrary library, Guid analysisId, CancellationToken ct = default) =>
        await library.RemoveAnalysisAsync(await AssistantRequest.HouseholdAsync(accessor), analysisId, ct) ? "Analyse supprimée." : throw AssistantRequest.NotFound("Analyse");

    [McpServerTool(Name = "list_body_measures", ReadOnly = true, Title = "Mesures corporelles")]
    [Description("Les mesures corporelles suivies (poids, tour de taille, fréquence cardiaque au repos… créées la première fois), leur cible " +
        "personnelle facultative et leurs valeurs, avec la position de chacune (below_target, in_target, above_target ; null sans cible). " +
        "Une mesure liée à la Garmin (source garmin_resting_heart_rate) reprend aussi la FC au repos importée chaque jour (valeurs source garmin, " +
        "sans id, non supprimables) ; une valeur notée à la main le même jour l'emporte.")]
    public static async Task<List<MeasureTypeDto>> ListBodyMeasures(IHttpContextAccessor accessor, AnalysisLibrary library, CancellationToken ct = default) =>
        await library.MeasuresAsync(await AssistantRequest.HouseholdAsync(accessor), ct);

    [McpServerTool(Name = "create_body_measure_type", Title = "Suivre une nouvelle mesure")]
    [Description("Ajoute une mesure corporelle à suivre (ex. Tension systolique, mmHg), quand list_body_measures ne l'a pas.")]
    public static async Task<MeasureTypeDto> CreateBodyMeasureType(IHttpContextAccessor accessor, AnalysisLibrary library, string name, string unit,
        [Description("Cible personnelle, minimum (facultatif).")] decimal? targetMin = null,
        [Description("Cible personnelle, maximum (facultatif).")] decimal? targetMax = null,
        CancellationToken ct = default) =>
        await library.CreateMeasureTypeAsync(await AssistantRequest.HouseholdAsync(accessor),
            AssistantRequest.Validated(new MeasureTypeRequest(name, unit, targetMin, targetMax)), ct);

    [McpServerTool(Name = "update_body_measure_type", Idempotent = true, Title = "Modifier une mesure suivie")]
    [Description("Modifie le nom, l'unité, la cible personnelle ou la liaison Garmin d'une mesure suivie ; un champ omis reste inchangé, " +
        "clearTarget = true retire la cible.")]
    public static async Task<MeasureTypeDto> UpdateBodyMeasureType(IHttpContextAccessor accessor, AnalysisLibrary library, Guid measureTypeId,
        string? name = null, string? unit = null, decimal? targetMin = null, decimal? targetMax = null, bool clearTarget = false,
        [Description("true : reprendre la FC au repos de la Garmin ; false : ne plus la reprendre ; omis : inchangé.")] bool? linkGarminRestingHeartRate = null,
        CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var t = (await library.MeasuresAsync(household, ct)).SingleOrDefault(x => x.Id == measureTypeId).OrNotFound("Mesure");
        var request = AssistantRequest.Validated(new MeasureTypeRequest(name ?? t.Name, unit ?? t.Unit,
            clearTarget ? targetMin : targetMin ?? t.TargetMin, clearTarget ? targetMax : targetMax ?? t.TargetMax,
            linkGarminRestingHeartRate switch { true => MeasureType.GarminRestingHeartRate, false => null, null => t.Source }));
        return (await library.UpdateMeasureTypeAsync(household, measureTypeId, request, ct)).OrNotFound("Mesure");
    }

    [McpServerTool(Name = "add_body_measure", Title = "Noter une mesure")]
    [Description("Note une valeur d'une mesure corporelle, dans son unité.")]
    public static async Task<MeasureTypeDto> AddBodyMeasure(IHttpContextAccessor accessor, AnalysisLibrary library, Guid measureTypeId, decimal value,
        [Description("Jour (aaaa-mm-jj) ; aujourd'hui par défaut.")] DateOnly? day = null, string? note = null, CancellationToken ct = default) =>
        await library.AddMeasurementAsync(await AssistantRequest.HouseholdAsync(accessor), AssistantRequest.Validated(new MeasurementRequest(measureTypeId, day, value, note)), ct);

    [McpServerTool(Name = "delete_body_measure", Destructive = true, Title = "Supprimer une mesure")]
    [Description("Supprime une valeur notée par erreur.")]
    public static async Task<string> DeleteBodyMeasure(IHttpContextAccessor accessor, AnalysisLibrary library, Guid measurementId, CancellationToken ct = default) =>
        await library.RemoveMeasurementAsync(await AssistantRequest.HouseholdAsync(accessor), measurementId, ct) ? "Mesure supprimée." : throw AssistantRequest.NotFound("Mesure");

    // Documents

    [McpServerTool(Name = "list_health_documents", ReadOnly = true, Title = "Documents de santé")]
    [Description("Les documents de santé (ordonnances, comptes rendus, résultats, bilans), du plus récent au plus ancien, avec leurs fichiers " +
        "et les analyses saisies depuis chacun (analysisIds vide = rien saisi). " + DocumentTypeHelp + " " + PrivacyNote)]
    public static async Task<List<HealthDocumentDto>> ListHealthDocuments(IHttpContextAccessor accessor, HealthDocumentLibrary library,
        [Description(DocumentTypeHelp + " Vide = tous.")] string? type = null,
        [Description("Texte cherché dans le titre, le médecin et les notes.")] string? query = null,
        CancellationToken ct = default) =>
        await library.ListAsync(await AssistantRequest.HouseholdAsync(accessor), type, query, ct);

    [McpServerTool(Name = "get_health_document_file", ReadOnly = true, Title = "Lire un fichier de document")]
    [Description("Renvoie un fichier d'un document de santé (photo ou PDF) pour le lire. Pour un résultat d'analyse : proposez les " +
        "marqueurs et valeurs lus (list_markers pour réutiliser les existants), faites confirmer l'utilisateur, puis create_analysis " +
        "avec documentId.")]
    public static async Task<CallToolResult> GetHealthDocumentFile(IHttpContextAccessor accessor, HealthDocumentLibrary library,
        Guid documentId, Guid fileId, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var document = (await library.GetAsync(household, documentId, ct)).OrNotFound("Document");
        var file = (await library.FileAsync(household, documentId, fileId, ct)).OrNotFound("Fichier");
        var context = $"Document {document.Id} : {document.Title} ({document.Type}) du {document.Day:yyyy-MM-dd}" +
            $"{(document.Who is null ? "" : $", {document.Who}")} ; fichier {fileId}.";
        ContentBlock content = file.ContentType == "application/pdf"
            ? new EmbeddedResourceBlock { Resource = BlobResourceContents.FromBytes(file.Bytes, $"lifeos://health/documents/{documentId}/files/{fileId}", file.ContentType) }
            : ImageContentBlock.FromBytes(file.Bytes, file.ContentType);
        return new CallToolResult { Content = [new TextContentBlock { Text = context }, content] };
    }

    [McpServerTool(Name = "create_health_document", Title = "Créer un document de santé")]
    [Description("Crée la fiche d'un document de santé (sans fichier : les photos et PDF s'ajoutent dans l'application). " + DocumentTypeHelp)]
    public static async Task<HealthDocumentDto> CreateHealthDocument(IHttpContextAccessor accessor, HealthDocumentLibrary library,
        [Description(DocumentTypeHelp)] string type, string title,
        [Description("Date du document (aaaa-mm-jj) ; aujourd'hui par défaut.")] DateOnly? day = null,
        [Description("Médecin, spécialité ou laboratoire.")] string? who = null, string? notes = null, CancellationToken ct = default) =>
        await library.CreateAsync(await AssistantRequest.HouseholdAsync(accessor), AssistantRequest.Validated(new HealthDocumentRequest(type, title, day, who, notes)), ct);

    [McpServerTool(Name = "update_health_document", Idempotent = true, Title = "Modifier un document de santé")]
    [Description("Modifie la fiche d'un document ; un champ omis reste inchangé. " + DocumentTypeHelp)]
    public static async Task<HealthDocumentDto> UpdateHealthDocument(IHttpContextAccessor accessor, HealthDocumentLibrary library, Guid documentId,
        string? type = null, string? title = null, DateOnly? day = null, string? who = null, string? notes = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var d = (await library.GetAsync(household, documentId, ct)).OrNotFound("Document");
        var request = AssistantRequest.Validated(new HealthDocumentRequest(type ?? d.Type, title ?? d.Title, day ?? d.Day, who ?? d.Who, notes ?? d.Notes));
        return (await library.UpdateAsync(household, documentId, request, ct)).OrNotFound("Document");
    }

    // Appointments

    [McpServerTool(Name = "list_appointments", ReadOnly = true, Title = "Rendez-vous médicaux")]
    [Description("Les rendez-vous médicaux à venir (du plus proche) et passés (du plus récent). Consultez-les avant d'en créer un depuis " +
        "un e-mail ou l'agenda, pour mettre à jour un rendez-vous déplacé plutôt que d'en créer un doublon. " + PrivacyNote)]
    public static async Task<AppointmentListDto> ListAppointments(IHttpContextAccessor accessor, AppointmentLibrary library, CancellationToken ct = default) =>
        await library.ListAsync(await AssistantRequest.HouseholdAsync(accessor), ct);

    [McpServerTool(Name = "create_appointment", Title = "Créer un rendez-vous")]
    [Description("Crée un rendez-vous médical, par exemple depuis un e-mail de confirmation (Doctolib ou autre) après accord de l'utilisateur. " +
        "LifeOS ne se connecte à aucun site de réservation.")]
    public static async Task<AppointmentDto> CreateAppointment(IHttpContextAccessor accessor, AppointmentLibrary library,
        [Description("Jour (aaaa-mm-jj).")] DateOnly day,
        [Description("Médecin et/ou spécialité.")] string who,
        [Description("Heure (HH:mm).")] TimeOnly? time = null,
        string? reason = null, string? place = null, string? notes = null,
        [Description("Page de prise de rendez-vous du médecin (https), pour reprendre rendez-vous.")] string? bookingUrl = null,
        [Description("Compte rendu : un document de santé (list_health_documents).")] Guid? reportDocumentId = null,
        CancellationToken ct = default) =>
        await library.CreateAsync(await AssistantRequest.HouseholdAsync(accessor),
            AssistantRequest.Validated(new AppointmentRequest(day, time, who, reason, place, notes, bookingUrl, reportDocumentId)), ct);

    [McpServerTool(Name = "update_appointment", Idempotent = true, Title = "Modifier un rendez-vous")]
    [Description("Modifie un rendez-vous (déplacé, compte rendu reçu…) ; un champ omis reste inchangé.")]
    public static async Task<AppointmentDto> UpdateAppointment(IHttpContextAccessor accessor, AppointmentLibrary library, Guid appointmentId,
        DateOnly? day = null, TimeOnly? time = null, string? who = null, string? reason = null, string? place = null, string? notes = null,
        string? bookingUrl = null, Guid? reportDocumentId = null, CancellationToken ct = default)
    {
        var household = await AssistantRequest.HouseholdAsync(accessor);
        var list = await library.ListAsync(household, ct);
        var a = list.Upcoming.Concat(list.Past).SingleOrDefault(x => x.Id == appointmentId).OrNotFound("Rendez-vous");
        var request = AssistantRequest.Validated(new AppointmentRequest(day ?? a.Day, time ?? a.Time, who ?? a.Who, reason ?? a.Reason, place ?? a.Place,
            notes ?? a.Notes, bookingUrl ?? a.BookingUrl, reportDocumentId ?? a.ReportDocumentId));
        return (await library.UpdateAsync(household, appointmentId, request, ct)).OrNotFound("Rendez-vous");
    }

    [McpServerTool(Name = "delete_appointment", Destructive = true, Title = "Supprimer un rendez-vous")]
    [Description("Supprime un rendez-vous annulé.")]
    public static async Task<string> DeleteAppointment(IHttpContextAccessor accessor, AppointmentLibrary library, Guid appointmentId, CancellationToken ct = default) =>
        await library.RemoveAsync(await AssistantRequest.HouseholdAsync(accessor), appointmentId, ct) ? "Rendez-vous supprimé." : throw AssistantRequest.NotFound("Rendez-vous");
}
