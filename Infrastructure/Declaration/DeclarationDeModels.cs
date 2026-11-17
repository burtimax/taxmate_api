namespace Infrastructure.Declaration;

public sealed class CurrencyIncomeItem
{
    public string SourseName { get; set; } = "";
    public string CountryCode1 { get; set; } = "784";
    public string CountryCode2 { get; set; } = "784";
    public DateTime IncomeGetDate { get; set; }
    public DateTime HoldDate { get; set; }
    public string CurrencyCode { get; set; } = "840";
    public string CurrencyName { get; set; } = "Доллар США";
    public decimal CurrencyExchange { get; set; }
    public int CurrencyUnits { get; set; } = 100;
    public decimal CurrencyExchangeHoldDate { get; set; }
    public int CurrencyUnitsHoldDate { get; set; } = 100;
    public decimal IncomeRoubleSum { get; set; }
    public decimal TaxCurrencySum { get; set; }
    public decimal TaxRoubleSum { get; set; }
    public bool IsAutoExchange { get; set; }
    public int TaxRate { get; set; }
    public int KIKIncomeType { get; set; }
    public string KIKNum { get; set; } = "";
    public decimal TaxKIKHoldRF { get; set; }
    public string IncomeCod { get; set; } = "";
    public decimal IncomeSum { get; set; }
    public string SubtractCod { get; set; } = "";
    public decimal SubtractSum { get; set; }
}

public sealed class PersonInfoBlock
{
    public string SurName { get; set; } = "";
    public string Name { get; set; } = "";
    public string Patronymic { get; set; } = "";
    public string INN { get; set; } = "";
    public string DocType { get; set; } = "";
    public string SerialNum { get; set; } = "";
    public DateTime BirthDate { get; set; } = new(1970, 1, 1);
    public string CountryCodId { get; set; } = "643";
    public string Phone { get; set; } = "";
    public string OKTMO { get; set; } = "";
    public string ERN { get; set; } = "";
}

/// <summary>Корневой документ .de4 (2024).</summary>
public sealed class DeclarationDe4Document
{
    public PersonInfoBlock PersonInfo { get; set; } = new();
    public object Business { get; set; } = EmptyBusiness();
    public object ConstructionDeduct { get; set; } = EmptyConstruction();
    public object SocialDeduct { get; set; } = EmptySocial();
    public object StandartDeduct { get; set; } = EmptyStandartDe4();
    public object PastYearSecurities { get; set; } = new { SecuritiesSum = 0.0, FissSum = 0.0, InvestmentPartnershipSum = 0.0 };
    public object InvestmentDeduct { get; set; } = EmptyInvestment();
    public object LongTermDeduct { get; set; } = EmptyLongTerm();
    public string InspectionNumber { get; set; } = "";
    public int NumberDeclVersion { get; set; }
    public int PersonKind { get; set; } = 760;
    public int DeclType { get; set; }
    public int ContentSet { get; set; } = 10;
    public int DeclGrantingType { get; set; } = 1;
    public string RepresentativeName { get; set; } = "";
    public string RepresentativeSurName { get; set; } = "";
    public string RepresentativePatronymic { get; set; } = "";
    public string RepresentativeDoc { get; set; } = "";
    public object[] SourseInfoList { get; set; } = [];
    public List<CurrencyIncomeItem> CurrencyIncomeList { get; set; } = [];
    public bool IsFixKIKUsage { get; set; }
    public bool IsSocialDeducts { get; set; }
    public bool IsInvestmentDeducts { get; set; }
    public bool IsLongTermDeducts { get; set; }
    public bool IsPastYearSecurities { get; set; }
    public bool IsConstructionDeducts { get; set; }
    public bool IsStandartDeducts { get; set; }
    public decimal FixedPaymentSum { get; set; }
    public string BIK { get; set; } = "";
    public int AccountType { get; set; }
    public string AccountNumber { get; set; } = "";
    public decimal ReturnSum { get; set; }
    public int SourseInfoQuantity { get; set; }
    public int CurrencyQuantity { get; set; }

    private static object EmptyBusiness() => new
    {
        PatentPaymentSum = 0.0,
        TradeTaxSum = 0.0,
        AdjustedIncomeSum = 0.0,
        AdjustedTaxSum = 0.0,
        ActualPayments = 0.0,
        IsDifferentOKTMO = false,
        OKTMO1 = "",
        OKTMO2 = "",
        OKTMO3 = "",
        IsKFH = false,
        YearKFHBegin = 0,
        BusinessIncomeList = Array.Empty<object>(),
        BusinessQuantity = 0
    };

    private static object EmptyConstruction() => new
    {
        ConstructionObjectsList = Array.Empty<object>(),
        IsPensioner = false,
        LastYearsSum = 0.0,
        LastYearsCreditSum = 0.0,
        SimplifiedDeductSum = 0.0,
        SimplifiedCreditDeductSum = 0.0,
        SimplifiedReturnSum = 0.0,
        SimplifiedCreditReturnSum = 0.0,
        ConstructionObjectsQuantity = 0
    };

    private static object EmptySocial() => new
    {
        CharitySum = 0.0,
        Charity30Sum = 0.0,
        SelfEducationSum = 0.0,
        SelfEducationForeignSum = 0.0,
        ChildEducation = Array.Empty<object>(),
        MedicalSum = 0.0,
        ExpensiveMedicalSum = 0.0,
        MedicamentSum = 0.0,
        QualificationSum = 0.0,
        PhysicalAndWellnessSum = 0.0,
        SimplePensionSum = 0.0,
        SimplifiedDeductSum = 0.0,
        SimplifiedReturnSum = 0.0,
        PensionPaymentsList = Array.Empty<object>(),
        PensionPaymentsQuantity = 0,
        ChildEducationQuantity = 0
    };

    private static object EmptyStandartDe4() => new
    {
        ChildInfoList = Array.Empty<object>(),
        S010Sum = 0.0,
        S020Sum = 0.0,
        S030Sum = 0.0,
        S040Sum = 0.0,
        S050Sum = 0.0,
        S060Sum = 0.0,
        Deduct218 = 0,
        Jan = 0,
        Feb = 0,
        Mar = 0,
        Apr = 0,
        May = 0,
        Jun = 0,
        Jul = 0,
        Aug = 0,
        Sep = 0,
        Oct = 0,
        Nov = 0,
        Dec = 0
    };

    private static object EmptyInvestment() => new
    {
        P1SecuritiesSum = 0.0,
        P1InvestmentPartnershipsSum = 0.0,
        P2Return = 0.0,
        IsP3 = false,
        IndividualInvestmentAccountsList = Array.Empty<object>(),
        IndividualInvestmentAccountQuantity = 0
    };

    private static object EmptyLongTerm() => new
    {
        NewIndividualInvestmentAccountsList = Array.Empty<object>(),
        LongTermAgreementsList = Array.Empty<object>(),
        IndividualInvestmentAccountQuantity = 0,
        LongTermAgreementsQuantity = 0
    };
}

/// <summary>Корневой документ .de5 (2025+).</summary>
public sealed class DeclarationDe5Document
{
    public PersonInfoBlock PersonInfo { get; set; } = new();
    public object Business { get; set; } = DeclarationDe4DocumentHelpers.EmptyBusiness();
    public object ConstructionDeduct { get; set; } = DeclarationDe4DocumentHelpers.EmptyConstruction();
    public object SocialDeduct { get; set; } = DeclarationDe4DocumentHelpers.EmptySocial();
    public object StandartDeduct { get; set; } = EmptyStandartDe5();
    public object PastYearSecurities { get; set; } = new { SecuritiesSum = 0.0, FissSum = 0.0, InvestmentPartnershipSum = 0.0 };
    public object InvestmentDeduct { get; set; } = DeclarationDe4DocumentHelpers.EmptyInvestment();
    public object LongTermDeduct { get; set; } = DeclarationDe4DocumentHelpers.EmptyLongTerm();
    public string InspectionNumber { get; set; } = "";
    public int NumberDeclVersion { get; set; }
    public int PersonKind { get; set; } = 760;
    public int DeclType { get; set; }
    public int ContentSet { get; set; } = 10;
    public int DeclGrantingType { get; set; } = 1;
    public string RepresentativeName { get; set; } = "";
    public string RepresentativeSurName { get; set; } = "";
    public string RepresentativePatronymic { get; set; } = "";
    public string RepresentativeDoc { get; set; } = "";
    public object[] SourseInfoList { get; set; } = [];
    public List<CurrencyIncomeItem> CurrencyIncomeList { get; set; } = [];
    public bool IsFixKIKUsage { get; set; }
    public bool IsSocialDeducts { get; set; }
    public bool IsInvestmentDeducts { get; set; }
    public bool IsLongTermDeducts { get; set; }
    public bool IsPastYearSecurities { get; set; }
    public bool IsConstructionDeducts { get; set; }
    public bool IsStandartDeducts { get; set; }
    public decimal FixedPaymentSum { get; set; }
    public string BIK { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public decimal ReturnSum { get; set; }
    public string Card { get; set; } = "";
    public int SourseInfoQuantity { get; set; }
    public int CurrencyQuantity { get; set; }

    private static object EmptyStandartDe5() => new
    {
        ChildInfoList = Array.Empty<object>(),
        IsGTO = false,
        KvGTO = 0,
        Deduct218 = 0,
        Jan = 0,
        Feb = 0,
        Mar = 0,
        Apr = 0,
        May = 0,
        Jun = 0,
        Jul = 0,
        Aug = 0,
        Sep = 0,
        Oct = 0,
        Nov = 0,
        Dec = 0
    };
}

/// <summary>Общие фабрики пустых блоков (чтобы не дублировать в De5).</summary>
internal static class DeclarationDe4DocumentHelpers
{
    public static object EmptyBusiness() => new
    {
        PatentPaymentSum = 0.0,
        TradeTaxSum = 0.0,
        AdjustedIncomeSum = 0.0,
        AdjustedTaxSum = 0.0,
        ActualPayments = 0.0,
        IsDifferentOKTMO = false,
        OKTMO1 = "",
        OKTMO2 = "",
        OKTMO3 = "",
        IsKFH = false,
        YearKFHBegin = 0,
        BusinessIncomeList = Array.Empty<object>(),
        BusinessQuantity = 0
    };

    public static object EmptyConstruction() => new
    {
        ConstructionObjectsList = Array.Empty<object>(),
        IsPensioner = false,
        LastYearsSum = 0.0,
        LastYearsCreditSum = 0.0,
        SimplifiedDeductSum = 0.0,
        SimplifiedCreditDeductSum = 0.0,
        SimplifiedReturnSum = 0.0,
        SimplifiedCreditReturnSum = 0.0,
        ConstructionObjectsQuantity = 0
    };

    public static object EmptySocial() => new
    {
        CharitySum = 0.0,
        Charity30Sum = 0.0,
        SelfEducationSum = 0.0,
        SelfEducationForeignSum = 0.0,
        ChildEducation = Array.Empty<object>(),
        MedicalSum = 0.0,
        ExpensiveMedicalSum = 0.0,
        MedicamentSum = 0.0,
        QualificationSum = 0.0,
        PhysicalAndWellnessSum = 0.0,
        SimplePensionSum = 0.0,
        SimplifiedDeductSum = 0.0,
        SimplifiedReturnSum = 0.0,
        PensionPaymentsList = Array.Empty<object>(),
        PensionPaymentsQuantity = 0,
        ChildEducationQuantity = 0
    };

    public static object EmptyInvestment() => new
    {
        P1SecuritiesSum = 0.0,
        P1InvestmentPartnershipsSum = 0.0,
        P2Return = 0.0,
        IsP3 = false,
        IndividualInvestmentAccountsList = Array.Empty<object>(),
        IndividualInvestmentAccountQuantity = 0
    };

    public static object EmptyLongTerm() => new
    {
        NewIndividualInvestmentAccountsList = Array.Empty<object>(),
        LongTermAgreementsList = Array.Empty<object>(),
        IndividualInvestmentAccountQuantity = 0,
        LongTermAgreementsQuantity = 0
    };
}
