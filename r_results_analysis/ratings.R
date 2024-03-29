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
data <- read.csv("ratings_final.csv", stringsAsFactors = TRUE)
head(data)

# replace the value gpt with LLM
data$serving_strategy <- ifelse(data$serving_strategy == "gpt", "LLM", data$serving_strategy)

# replace the value random with traditional
data$serving_strategy <- ifelse(data$serving_strategy == "2", "traditional", data$serving_strategy)

# means
mean(data$rating[data$serving_strategy == "LLM"])
mean(data$rating[data$serving_strategy == "traditional"])

# histograms
ggplot(data, aes(x = rating)) +
    geom_histogram(bins = 5) +
    facet_grid(serving_strategy ~ .) +
    ylab("") +
    xlab("Rating")

# save in a 1920x1080 pixel png file
ggsave("ratings_histogram.png", width = 1920, height = 1080, dpi = 300, units = "px")

# stan -------------------------------------------------------------------------
# model
model <- cmdstan_model("ordered.stan")

# encode LLM as 1 and traditional as 0
x <- ifelse(data$serving_strategy == "LLM", 1, 0)

# dependent variable
y <- data$rating

# stan_data
stan_data <- list(n = nrow(data), k = 5, x = x, y = y)

# fit
fit <- model$sample(
    data = stan_data,
    parallel_chains = 4
)

# diagnostics ------------------------------------------------------------------
# traceplot for beta parameters
mcmc_trace(fit$draws("beta"))
mcmc_trace(fit$draws("c"))

# summary of betas
fit$summary("beta")
fit$summary("c")

# analysis ---------------------------------------------------------------------
# extract parameters
df_beta <- as_draws_df(fit$draws("beta"))
df_beta <- df_beta %>% select(-.chain, -.iteration, -.draw)
df_cutpoints <- as_draws_df(fit$draws("c"))
df_cutpoints <- df_cutpoints %>% select(-.chain, -.iteration, -.draw)

# plot betas -------------------------------------------------------------------
# rename for ease of addressing
colnames(df_beta) <- c("LLM")
mcse(df_beta$LLM < 0) # P = 0.99 +/- 0.01

# to long format
df_beta_long <- df_beta %>% gather(Beta, Value)

# plot
ggplot(data = df_beta_long, aes(x = Value, y = Beta)) +
    stat_eye(fill = "skyblue", alpha = 0.75) +
    xlim(-1.6, 1.6)

# save in a 1920x1080 pixel png file
ggsave("ratings.png", width = 1920, height = 1080, dpi = 300, units = "px")

# analysis of LLM ratings for first level and the rest -------------------------
data_llm <- data %>% filter(serving_strategy == "LLM")

# other level is 1 if it is not the first
data_llm$other_level <- ifelse(data_llm$level != 1, 1, 0)
data_llm$level_text <- ifelse(data_llm$level != 1, "Other", "First")

mean(data_llm$rating[data_llm$other_level == 1])
mean(data_llm$rating[data_llm$other_level == 0])

# histograms
ggplot(data_llm, aes(x = rating)) +
    geom_histogram(bins = 5) +
    facet_grid(level_text ~ .) +
    ylab("") +
    xlab("Rating")

# save in a 1920x1080 pixel png file
ggsave("ratings_histogram_other_level.png", width = 1920, height = 1080, dpi = 300, units = "px")

# encode LLM as 1 and traditional as 0
x <- data_llm$other_level

# dependent variable
y <- data_llm$rating

# stan_data
stan_data <- list(n = nrow(data_llm), k = 5, x = x, y = y)

# fit
fit_llm <- model$sample(
    data = stan_data,
    parallel_chains = 4
)

# diagnostics ------------------------------------------------------------------
# traceplot for beta parameters
mcmc_trace(fit_llm$draws("beta"))
mcmc_trace(fit_llm$draws("c"))

# summary of betas
fit_llm$summary("beta")
fit_llm$summary("c")

# analysis ---------------------------------------------------------------------
# extract parameters
df_beta_llm <- as_draws_df(fit_llm$draws("beta"))
df_beta_llm <- df_beta_llm %>% select(-.chain, -.iteration, -.draw)
df_cutpoints_llm <- as_draws_df(fit_llm$draws("c"))
df_cutpoints_llm <- df_cutpoints_llm %>% select(-.chain, -.iteration, -.draw)

# plot betas -------------------------------------------------------------------
# rename for ease of addressing
colnames(df_beta_llm) <- c("other_level")
mcse(df_beta_llm$other_level > 0) # P = 0.99 +/- 0.01

# to long format
df_beta_llm_long <- df_beta_llm %>% gather(Beta, Value)

# plot
ggplot(data = df_beta_llm_long, aes(x = Value, y = Beta)) +
    stat_eye(fill = "skyblue", alpha = 0.75) +
    xlim(-1.6, 1.6)

# save in a 1920x1080 pixel png file
ggsave("ratings_other_level.png", width = 1920, height = 1080, dpi = 300, units = "px")

# analysis by adding 0 values for abandoned runs -------------------------------
level_data <- read.csv("level_data_final.csv", stringsAsFactors = TRUE)
level_data$serving_strategy <-
    ifelse(level_data$serving_strategy == "gpt", "LLM", level_data$serving_strategy)
level_data$serving_strategy <-
    ifelse(level_data$serving_strategy == "2", "traditional", level_data$serving_strategy)

# get number of abandoned runs
rated_llm <- sum(data$serving_strategy == "LLM")
total_llm <- (level_data %>%
    filter(
        serving_strategy == "LLM",
        level_group == "all_levels",
        event_name == "level_start"))$cnt
diff_llm <- total_llm - rated_llm

rated_traditional <- sum(data$serving_strategy == "traditional")
total_traditional <- (level_data %>%
    filter(
        serving_strategy == "traditional",
        level_group == "all_levels",
        event_name == "level_start"))$cnt
diff_traditional <- total_traditional - rated_traditional

# append diff_llm and diff_traditional to data
data_abandoned <- data %>% add_row(data.frame(
    serving_strategy = rep("LLM", diff_llm),
    rating = rep(0, diff_llm),
    level = rep(0, diff_llm)
))
data_abandoned <- data %>% add_row(data.frame(
    serving_strategy = rep("traditional", diff_traditional),
    rating = rep(0, diff_traditional),
    level = rep(0, diff_traditional)
))

# means
mean(data_abandoned$rating[data$serving_strategy == "LLM"])
mean(data_abandoned$rating[data$serving_strategy == "traditional"])

# histograms
ggplot(data_abandoned, aes(x = rating)) +
    geom_histogram(bins = 6) +
    facet_grid(serving_strategy ~ .) +
    ylab("") +
    xlab("Rating")

# save in a 1920x1080 pixel png file
ggsave("ratings_histogram_abandoned.png", width = 1920, height = 1080, dpi = 300, units = "px")

# fit --------------------------------------------------------------------------
# encode LLM as 1 and traditional as 0
x <- ifelse(data_abandoned$serving_strategy == "LLM", 1, 0)

# dependent variable
y <- data_abandoned$rating + 1

# stan_data
stan_data <- list(n = nrow(data_abandoned), k = 6, x = x, y = y)

# fit
fit_abanoned <- model$sample(
    data = stan_data,
    parallel_chains = 4
)

# diagnostics ------------------------------------------------------------------
# traceplot for beta parameters
mcmc_trace(fit_abanoned$draws("beta"))
mcmc_trace(fit_abanoned$draws("c"))

# summary of betas
fit_abanoned$summary("beta")
fit_abanoned$summary("c")

# analysis ---------------------------------------------------------------------
# extract parameters
df_beta_abandoned <- as_draws_df(fit_abanoned$draws("beta"))
df_beta_abandoned <- df_beta_abandoned %>% select(-.chain, -.iteration, -.draw)
df_cutpoints_abandoned <- as_draws_df(fit_abanoned$draws("c"))
df_cutpoints_abandoned <- df_cutpoints_abandoned %>% select(-.chain, -.iteration, -.draw)

# plot betas -------------------------------------------------------------------
# rename for ease of addressing
colnames(df_beta_abandoned) <- c("LLM")
mcse(df_beta_abandoned$LLM > 0) # P = 0.99 +/- 0.01

# to long format
df_beta_abandoned_long <- df_beta_abandoned %>% gather(Beta, Value)

# plot
ggplot(data = df_beta_abandoned_long, aes(x = Value, y = Beta)) +
    stat_eye(fill = "skyblue", alpha = 0.75) +
    xlim(-1.6, 1.6)

# save in a 1920x1080 pixel png file
ggsave("ratings_abanoned.png", width = 1920, height = 1080, dpi = 300, units = "px")
