using System.Globalization;

namespace HealthScore.Infrastructure;

public static class FarmaSalesforceQueries
{
    public static string Accounts(DateTime? since)
    {
        var incrementalFilter = since.HasValue ? $" AND SystemModstamp > {Format(since.Value)}" : string.Empty;
        return $"""
        SELECT Id, Name, ParentId, Parent.Name, CpfCnpj__c, GrupoEconomico__c, Marca__c, Vertical__c, Status__c,
               CreatedDate, SystemModstamp
        FROM Account
        WHERE Vertical__c = 'FARMA'{incrementalFilter}
        ORDER BY SystemModstamp ASC
        """;
    }

    public static string Cases(DateTime since) => $"""
        SELECT Id, CaseNumber, AccountId, CreatedDate, ClosedDate, SystemModstamp, Status, Priority,
               SLA_violado__c, FCR_Formula__c, Issue_Code_Jira__c, Issue_type_JIRA__c,
               Produto__c, Vertical_de_Abertura__c, GrupoEconomico__c, Marca__c,
               Nivel_1__c, Nivel_2__c, Nivel_3__c, Nivel_4__c, Descricao_Taxonomia__c
        FROM Case
        WHERE Account.Vertical__c = 'FARMA' AND SystemModstamp > {Format(since)}
        ORDER BY SystemModstamp ASC
        """;

    private static string Format(DateTime value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
