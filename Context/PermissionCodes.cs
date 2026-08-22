namespace AgendaApp.Context
{
    public static class PermissionCodes
    {
        public const string BusinessEdit = "business.edit";
        public const string CollaboratorsManage = "collaborators.manage";
        public const string ServicesManage = "services.manage";
        public const string AccountingView = "accounting.view";
        public const string StatsView = "stats.view";
        public const string ClientsContactsView = "clients.contacts.view";
        public const string FlexibilityApprove = "flexibility.approve";

        public static readonly IReadOnlyDictionary<string, string> Catalog = new Dictionary<string, string>
        {
            [BusinessEdit] = "Editar datos de la empresa",
            [CollaboratorsManage] = "Gestionar colaboradores (crear/editar perfiles)",
            [ServicesManage] = "Gestionar servicios (crear/editar catálogo)",
            [AccountingView] = "Ver contabilidad",
            [StatsView] = "Ver estadísticas de flujo de clientes",
            [ClientsContactsView] = "Ver contactos de clientes",
            [FlexibilityApprove] = "Aprobar políticas de flexibilidad (horario, turnos fuera de horario)"
        };
    }
}
