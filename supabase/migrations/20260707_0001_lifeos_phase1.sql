create table if not exists public.user_settings (
  user_id uuid primary key references auth.users (id) on delete cascade,
  data jsonb not null default '{}'::jsonb,
  updated_at timestamptz not null default now()
);

create table if not exists public.planning_rules (
  id text primary key,
  user_id uuid not null references auth.users (id) on delete cascade,
  rule_kind text not null check (rule_kind in ('planning', 'frequency')),
  data jsonb not null,
  updated_at timestamptz not null default now()
);

create index if not exists planning_rules_user_id_idx
  on public.planning_rules (user_id);

create table if not exists public.week_context_config (
  user_id uuid primary key references auth.users (id) on delete cascade,
  reference_week_start_date date not null,
  reference_week_mode text not null check (reference_week_mode in ('kids', 'solo')),
  updated_at timestamptz not null default now()
);

create table if not exists public.week_mode_overrides (
  user_id uuid not null references auth.users (id) on delete cascade,
  week_start_date date not null,
  mode text not null check (mode in ('kids', 'solo')),
  updated_at timestamptz not null default now(),
  primary key (user_id, week_start_date)
);

create index if not exists week_mode_overrides_user_id_idx
  on public.week_mode_overrides (user_id);

create table if not exists public.week_plans (
  user_id uuid not null references auth.users (id) on delete cascade,
  week_start_date date not null,
  data jsonb not null,
  updated_at timestamptz not null default now(),
  primary key (user_id, week_start_date)
);

create index if not exists week_plans_user_id_week_start_date_idx
  on public.week_plans (user_id, week_start_date desc);

alter table public.user_settings enable row level security;
alter table public.planning_rules enable row level security;
alter table public.week_context_config enable row level security;
alter table public.week_mode_overrides enable row level security;
alter table public.week_plans enable row level security;

create policy "user_settings_select_own"
  on public.user_settings
  for select
  using ((select auth.uid()) = user_id);

create policy "user_settings_insert_own"
  on public.user_settings
  for insert
  with check ((select auth.uid()) = user_id);

create policy "user_settings_update_own"
  on public.user_settings
  for update
  using ((select auth.uid()) = user_id)
  with check ((select auth.uid()) = user_id);

create policy "user_settings_delete_own"
  on public.user_settings
  for delete
  using ((select auth.uid()) = user_id);

create policy "planning_rules_select_own"
  on public.planning_rules
  for select
  using ((select auth.uid()) = user_id);

create policy "planning_rules_insert_own"
  on public.planning_rules
  for insert
  with check ((select auth.uid()) = user_id);

create policy "planning_rules_update_own"
  on public.planning_rules
  for update
  using ((select auth.uid()) = user_id)
  with check ((select auth.uid()) = user_id);

create policy "planning_rules_delete_own"
  on public.planning_rules
  for delete
  using ((select auth.uid()) = user_id);

create policy "week_context_config_select_own"
  on public.week_context_config
  for select
  using ((select auth.uid()) = user_id);

create policy "week_context_config_insert_own"
  on public.week_context_config
  for insert
  with check ((select auth.uid()) = user_id);

create policy "week_context_config_update_own"
  on public.week_context_config
  for update
  using ((select auth.uid()) = user_id)
  with check ((select auth.uid()) = user_id);

create policy "week_context_config_delete_own"
  on public.week_context_config
  for delete
  using ((select auth.uid()) = user_id);

create policy "week_mode_overrides_select_own"
  on public.week_mode_overrides
  for select
  using ((select auth.uid()) = user_id);

create policy "week_mode_overrides_insert_own"
  on public.week_mode_overrides
  for insert
  with check ((select auth.uid()) = user_id);

create policy "week_mode_overrides_update_own"
  on public.week_mode_overrides
  for update
  using ((select auth.uid()) = user_id)
  with check ((select auth.uid()) = user_id);

create policy "week_mode_overrides_delete_own"
  on public.week_mode_overrides
  for delete
  using ((select auth.uid()) = user_id);

create policy "week_plans_select_own"
  on public.week_plans
  for select
  using ((select auth.uid()) = user_id);

create policy "week_plans_insert_own"
  on public.week_plans
  for insert
  with check ((select auth.uid()) = user_id);

create policy "week_plans_update_own"
  on public.week_plans
  for update
  using ((select auth.uid()) = user_id)
  with check ((select auth.uid()) = user_id);

create policy "week_plans_delete_own"
  on public.week_plans
  for delete
  using ((select auth.uid()) = user_id);
