using HealthScore.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthScore.Infrastructure.Migrations
{
    [DbContext(typeof(HealthScoreDbContext))]
    [Migration("20260710133500_EnsureProductControlSeedCorrections")]
    public partial class EnsureProductControlSeedCorrections : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS `IX_business_unit_controls_BusinessUnit` ON `business_unit_controls`;
                CREATE INDEX IF NOT EXISTS `IX_business_unit_controls_BusinessUnit`
                    ON `business_unit_controls` (`BusinessUnit`);
                CREATE UNIQUE INDEX IF NOT EXISTS `IX_business_unit_controls_BusinessUnit_Vertical_Product_Scope`
                    ON `business_unit_controls` (`BusinessUnit`, `Vertical`, `Product`, `Scope`);
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO `product_mappings` (`Vertical`, `StandardProduct`, `SourceSystem`, `SourceValue`, `CommercialProduct`, `UpdatedAt`, `UpdatedBy`) VALUES
                ('FARMA', 'BIG SISTEMAS', 'produto_salesforce', 'LINX FARMA BIG', 'LINX BIG FARMA', UTC_TIMESTAMP(6), 'manual:alias-salesforce-produto')
                ON DUPLICATE KEY UPDATE
                    `Vertical` = VALUES(`Vertical`),
                    `StandardProduct` = VALUES(`StandardProduct`),
                    `CommercialProduct` = VALUES(`CommercialProduct`),
                    `UpdatedAt` = VALUES(`UpdatedAt`),
                    `UpdatedBy` = VALUES(`UpdatedBy`);
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO `business_unit_controls` (`BusinessUnit`, `Vertical`, `Product`, `Scope`, `UpdatedAt`, `UpdatedBy`) VALUES
                ('ADESÃO - FARMA', 'FARMA', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('ADESÃO - FOOD', 'FOOD', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('ADESÃO - POSTOS', 'POSTOS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('ADESÃO - RETAIL', 'RETAIL', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('ADMINISTRATIVO/FINANCEIRO', 'ADMFIN', 'ADMFIN', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('APOIO FRANQUIAS', 'OUTROS', 'OUTROS', 'FRANQUIAS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('AUTOMOTIVO - ADMINISTRATIVO/FINANCEIRO', 'AUTOMOTIVO', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('AUTOMOTIVO - LINX AUTOSHOP', 'AUTOMOTIVO', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('AUTOMOTIVO - LINX DMS / LINX DMS HPE / LINX DMS TOYOTA', 'AUTOMOTIVO', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('AUTOMOTIVO - LINX DMS BRAVOS', 'AUTOMOTIVO', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('AUTOMOTIVO - LINX SMART API', 'AUTOMOTIVO', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('AUTOMOTIVO - SISDIA', 'AUTOMOTIVO', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('AUTOMOTIVO-DEMANDAS INTERNAS APOLLO', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('AUTOMOTIVO-DEMANDAS INTERNAS AUTOSHOP', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('AUTOMOTIVO-DEMANDAS INTERNAS BRAVOS', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('BACKOFFICE FOOD', 'FOOD', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('BRIDGE - CONECTIVIDADE', 'CONECTIVIDADE', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('BRIDGE - FISCAL FLOW', 'CROSS', 'PAYHUB', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('BRIDGE - HUMANUS', 'LINX PEOPLE', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('BRIDGE - HUMANUS CONSULTORIA', 'LINX PEOPLE', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('CALÇADOS - SETA DIGITAL', 'RETAIL', 'SETA', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('CLOUD OPERATIONS', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('COBRANÇA', 'ADMFIN', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('CONECTIVIDADE - ADMINISTRATIVO/FINANCEIRO', 'CONECTIVIDADE', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('CORP - TOOLS', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('CORPORATIVO - FRANQUIAS E PARCEIROS LSP', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('CORPORATIVO - PREMIER FRANQUIAS POSTOS', 'POSTOS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('CORPORATIVO - XCENTER CONEC', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('CROSS - ADMINISTRATIVO/ FINANCEIRO', 'CROSS', 'ADMFIN', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('CROSS-MAR_ABERTO (ANTIGO PAYHUB)', 'CROSS', 'MAR ABERTO', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('CROSS-VERTICAIS_LINX', 'CROSS', 'VERTICAIS', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('CS - NEEMO', 'FOOD', 'NEEMO', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('CS - POSTOS', 'POSTOS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('DEGUST', 'FOOD', 'DEGUST', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('DELIVERY CENTER - SHOPPING', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('DIGITAL - ADS', 'DIGITAL', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('DIGITAL - ECOMMERCE - B2C', 'DIGITAL', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('DIGITAL - ECOMMERCE - PARTNERS', 'DIGITAL', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('DIGITAL - IMPULSE', 'DIGITAL', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('DIGITAL - LINX COMMERCE', 'DIGITAL', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('DIGITAL - LINX COMMERCE FAST', 'DIGITAL', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('DIGITAL - LINX OMS', 'DIGITAL', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('EMAIL DESCARTADO', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('ENTERPRISE - ADMINISTRATIVO/FINANCEIRO', 'ENTERPRISE', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('ENTERPRISE - BIGRETAIL - WHITELABEL', 'ENTERPRISE', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('ENTERPRISE - DIGITAL - LINX COMMERCE', 'ENTERPRISE', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('ENTERPRISE - GESTÃO FRANQUIAS', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('ENTERPRISE - PAYHUB', 'ENTERPRISE', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('ENTERPRISE - RETENÇÃO', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('ENTERPRISE - SERVIÇOS', 'ENTERPRISE', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('ENTERPRISE - STOREX', 'ENTERPRISE', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('ENTERPRISE - STOREX HOME', 'ENTERPRISE', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('ENTERPRISE – QUALITEAM', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FARMA - ADMINISTRATIVO/FINANCEIRO', 'FARMA', 'ADMFIN', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FARMA - ALÔ FRANQUIAS', 'FARMA', 'OUTROS', 'ALÔ PARCEIROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FARMA - BIG SISTEMAS', 'FARMA', 'BIG SISTEMAS', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FARMA - CROSS', 'FARMA', 'OUTROS', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FARMA - FARMA CLOUD', 'FARMA', 'FARMA CLOUD', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FARMA - IMPLANTAÇÕES', 'FARMA', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FARMA - ITEC', 'FARMA', 'ITEC', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FARMA - OPERA VENDAS', 'FARMA', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FARMA - RETENÇÃO', 'FARMA', 'OUTROS', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FARMA - SOFTPHARMA', 'FARMA', 'SOFTPHARMA', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FOOD - ADMINISTRATIVO/FINANCEIRO', 'FOOD', 'ADMFIN', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FOOD - ALÔ FRANQUIAS', 'FOOD', 'OUTROS', 'ALÔ PARCEIROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FOOD - BERCARIO', 'FOOD', 'OUTROS', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FOOD - EMSYS FOOD', 'FOOD', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FOOD- Sala do Cliente - Food', 'FOOD', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FOOD-ADMFIN_RETENCAO', 'FOOD', 'ADMFIN', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FOOD-ADMFIN_RETENCAO', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FOOD-ALO_PARCEIRO', 'FOOD', 'OUTROS', 'ALÔ PARCEIROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FOOD-CS', 'FOOD', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FOOD-CS', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FOOD-DEGUST', 'FOOD', 'DEGUST', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FOOD-MENEW', 'FOOD', 'MENEW', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FOOD-NEEMO', 'FOOD', 'NEEMO', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FOOD-TASTE_ONE', 'FOOD', 'TASTE ONE', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FRANQUEADOS - ENTERPRISE - MODA LINX', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FRANQUEADOS - POSTOS - POSTO FÁCIL', 'POSTOS', 'POSTO FÁCIL', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FRANQUEADOS FARMA - BIG SISTEMAS', 'FARMA', 'BIG SISTEMAS', 'FRANQUIAS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FRANQUEADOS POSTOS - AUTOSYSTEM', 'POSTOS', 'AUTOSYSTEM', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FRANQUEADOS POSTOS - EMSYS', 'POSTOS', 'EMSYS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FRANQUEADOS POSTOS SELLER E EMPÓRIO', 'POSTOS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FRANQUIA - PROSPECT - EMPORIO PRO', 'POSTOS', 'MPC', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('FRANQUIA - PROSPECT - SELLER', 'POSTOS', 'SELLER', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('Guardiões - Qualidade', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('LINX DIGITAL - LIDERES', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('LINX DIGITAL - QUALIDADE', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('LINX PROMO SUPORTE', 'ENTERPRISE', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('MELHORIAS - OFERTAS SHOPPING', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('MELHORIAS - OFERTAS SHOPPING - MODA PREMIER', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('MENEW', 'FOOD', 'MENEW', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('MERCADAPP', 'DIGITAL', 'MERCADAPP', 'MERCADAPP', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('MERCADO DE PROXIMIDADE - EMPÓRIO POP', 'POSTOS', 'MPC', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('MERCADO DE PROXIMIDADE - EMPÓRIO PRÓ', 'POSTOS', 'MPC', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('MILLENNIUM', 'ENTERPRISE', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('MILLENNIUM - SAAS', 'ENTERPRISE', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('MILLENNIUM - SERVIÇOS', 'ENTERPRISE', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('MODA - EASY LINX', 'RETAIL', 'EASY LINX', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('MODA - LINX', 'ENTERPRISE', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('MODA - LINX - INTEGRAÇÕES', 'ENTERPRISE', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('MODA - LINX UX', 'ENTERPRISE', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('MODA - MICROVIX', 'RETAIL', 'MICROVIX', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('MODA - MICROVIX WEB', 'RETAIL', 'MICROVIX', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('MODA - RESHOP', 'RETAIL', 'RESHOP', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('MODA PREMIER - LINX', 'ENTERPRISE', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('MODA PREMIER - LINX UX', 'ENTERPRISE', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('MODA PREMIER - MICROVIX', 'RETAIL', 'MICROVIX PREMIER', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('MODA PREMIER - MONITORIA', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('NEEMO', 'FOOD', 'NEEMO', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('NODIS', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('OTICAS - MICROVIX', 'RETAIL', 'MICROVIX', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('PALIATIVO GO LIVE', 'FOOD', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('PAYHUB', 'CROSS', 'PAYHUB', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('PLUGG.TO', 'DIGITAL', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('PORTO ALEGRE - PAY HUB', 'CROSS', 'PAYHUB', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('PORTO ALEGRE - PAY HUB - FARMA', 'CROSS', 'PAYHUB', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('PORTO ALEGRE - PAY HUB 2', 'CROSS', 'PAYHUB', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('POSTOS - ADMINISTRATIVO/FINANCEIRO', 'POSTOS', 'ADMFIN', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('POSTOS - AUTOSYSTEM', 'POSTOS', 'AUTOSYSTEM', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('POSTOS - BERÇARIO', 'POSTOS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('POSTOS - EMSYS', 'POSTOS', 'EMSYS', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('POSTOS - POSTO FACIL', 'POSTOS', 'POSTO FÁCIL', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('POSTOS - POSTO POP', 'POSTOS', 'POSTO POP', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('POSTOS - SELLER', 'POSTOS', 'SELLER', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('POSTOS - SERVIÇOS', 'POSTOS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('POSTOS- ALÔ FRANQUIAS', 'POSTOS', 'OUTROS', 'ALÔ PARCEIROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('PROJETO DEXTER', 'RETAIL', 'OUTROS', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('QUALIDADE - CANAIS EXTERNOS', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('RETAIL - ADMINISTRATIVO/FINANCEIRO', 'RETAIL', 'ADMFIN', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('RETAIL - RESHOP', 'RETAIL', 'RESHOP', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('RETAIL- ALÔ FRANQUIAS', 'RETAIL', 'MICROVIX', 'ALÔ PARCEIROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('RETAL-SETA-CUSTOMIZAÇÃO', 'RETAIL', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('RETENÇÃO - LINX PAY', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('RETENÇÃO RC POSTOS', 'POSTOS', 'OUTROS', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('SHOPPING - CROSS', 'OUTROS', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('SPECIAL SERVICE - SETA', 'RETAIL', 'SETA SS', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('TASTE ONE', 'FOOD', 'TASTE ONE', 'RC', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_'),
                ('VAREJO SERVICOS', 'ENTERPRISE', 'OUTROS', 'OUTROS', UTC_TIMESTAMP(6), 'xlsx:_de-para_produto_')
                ON DUPLICATE KEY UPDATE
                    `Vertical` = VALUES(`Vertical`),
                    `Product` = VALUES(`Product`),
                    `Scope` = VALUES(`Scope`),
                    `UpdatedAt` = VALUES(`UpdatedAt`),
                    `UpdatedBy` = VALUES(`UpdatedBy`);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM `product_mappings`
                WHERE `UpdatedBy` = 'manual:alias-salesforce-produto'
                  AND `SourceValue` = 'LINX FARMA BIG';
                """);
        }
    }
}
