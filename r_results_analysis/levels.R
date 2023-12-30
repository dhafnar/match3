# libraries
library(tidyverse)
library(ggplot2)
library(ggdist)
library(cmdstanr)
library(bayesplot)
library(posterior)
library(mcmcse)
library(HDInterval)

# data prep --------------------------------------------------------------------
data <- read.csv("level_data_final.csv", stringsAsFactors = TRUE)

# model ------------------------------------------------------------------------
model <- cmdstan_model("bernoulli.stan")

# random -----------------------------------------------------------------------
df_random <- data %>%
    filter(levelServingStrategy == "random" & level_group == "all_levels")
started <- filter(df_random, event_name == "level_start")$cnt
ended <- filter(df_random, event_name == "level_end")$cnt
y_random <- c(rep(0, started - ended), rep(1, ended))
stan_data <- list(n = length(y_random), y = y_random)

# fit
fit_random <- model$sample(
    data = stan_data,
    parallel_chains = 4
)

mcmc_trace(fit_random$draws("theta"))
fit_random$summary("theta")

# random 1st level -------------------------------------------------------------
df_random_1st <- data %>%
    filter(levelServingStrategy == "random" & level_group == "first_level")
started <- filter(df_random_1st, event_name == "level_start")$cnt
ended <- filter(df_random_1st, event_name == "level_end")$cnt
y_random_1st <- c(rep(0, started - ended), rep(1, ended))
stan_data <- list(n = length(y_random_1st), y = y_random_1st)

# fit
fit_random_1st <- model$sample(
    data = stan_data,
    parallel_chains = 4
)

mcmc_trace(fit_random_1st$draws("theta"))
fit_random_1st$summary("theta")

# GPT --------------------------------------------------------------------------
df_gpt <- data %>%
    filter(levelServingStrategy == "gpt" & level_group == "all_levels")
started <- filter(df_gpt, event_name == "level_start")$cnt
ended <- filter(df_gpt, event_name == "level_end")$cnt
y_gpt <- c(rep(0, started - ended), rep(1, ended))
stan_data <- list(n = length(y_gpt), y = y_gpt)

# fit
fit_gpt <- model$sample(
    data = stan_data,
    parallel_chains = 4
)

mcmc_trace(fit_gpt$draws("theta"))
fit_gpt$summary("theta")

# GPT 1st level ----------------------------------------------------------------
df_gpt_1st <- data %>%
    filter(levelServingStrategy == "gpt" & level_group == "first_level")
started <- filter(df_gpt_1st, event_name == "level_start")$cnt
ended <- filter(df_gpt_1st, event_name == "level_end")$cnt
y_gpt_1st <- c(rep(0, started - ended), rep(1, ended))
stan_data <- list(n = length(y_gpt_1st), y = y_gpt_1st)

# fit
fit_gpt_1st <- model$sample(
    data = stan_data,
    parallel_chains = 4
)

mcmc_trace(fit_gpt_1st$draws("theta"))
fit_gpt_1st$summary("theta")

# analysis ---------------------------------------------------------------------
df_theta_random <- as_draws_df(fit_random$draws("theta"))
df_theta_gpt <- as_draws_df(fit_gpt$draws("theta"))

mcse(df_theta_gpt$theta > df_theta_random$theta) # P = 1
mcse(df_theta_random$theta) # P = 0.35 +/- 0.001
hdi(df_theta_random$theta) # [0.31, 0.40]
mcse(df_theta_gpt$theta) # P = 0.55 +/- 0.001
hdi(df_theta_gpt$theta) # [0.51, 0.60]

df_theta_random_1st <- as_draws_df(fit_random_1st$draws("theta"))
df_theta_gpt_1st <- as_draws_df(fit_gpt_1st$draws("theta"))

mcse(df_theta_gpt_1st$theta > df_theta_random_1st$theta) # P = 1
mcse(df_theta_random_1st$theta) # P = 0.18 +/- 0.001
hdi(df_theta_random_1st$theta) # [0.14, 0.22]
mcse(df_theta_gpt_1st$theta) # P = 0.34 +/- 0.001
hdi(df_theta_gpt_1st$theta) # [0.29, 0.40]

# plot -------------------------------------------------------------------------
df_plot <- data.frame(
    theta = df_theta_random_1st$theta,
    level = "First level",
    PCG = "traditional"
)

df_plot <- df_plot %>% add_row(data.frame(
    theta = df_theta_gpt_1st$theta,
    level = "First level",
    PCG = "LLM"
))

df_plot <- df_plot %>% add_row(data.frame(
    theta = df_theta_random$theta,
    level = "All levels",
    PCG = "traditional"
))

df_plot <- df_plot %>% add_row(data.frame(
    theta = df_theta_gpt$theta,
    level = "All levels",
    PCG = "LLM"
))

# plot
ggplot(data = df_plot, aes(x = theta, y = PCG)) +
    stat_eye(fill = "skyblue", alpha = 0.75) +
    facet_grid(level ~ .) +
    xlab("Completition probability") +
    xlim(0, 1)

# save in a 1920x1080 pixel png file
ggsave("levels.png", width = 1920, height = 1080, dpi = 300, units = "px")
