@estimator @public
Feature: Pre-eligibility estimator
  A member of the public can find out, without signing in, roughly how much
  monthly income assistance they might receive. The estimate is computed in
  the browser from the published rate table and nothing is stored.

  Background:
    Given the estimator serves the seeded form and the August 2023 rates
    And I open the eligibility estimator

  Scenario: Only the residency question is asked at first
    Then I see the question "Do you currently reside in British Columbia?"
    But I do not see the question "Do you have a status that allows you to live in Canada?"

  Scenario: Not living in British Columbia ends the estimate
    When I answer "Do you currently reside in British Columbia?" with "No"
    Then I am warned that I might not be eligible
    And I do not see the question "Do you have a status that allows you to live in Canada?"

  Scenario: No eligible status ends the estimate
    When I answer "Do you currently reside in British Columbia?" with "Yes"
    And I answer "Do you have a status that allows you to live in Canada?" with "No"
    Then I am warned that I might not be eligible
    And the warning links to the residency requirements
    And I do not see the question "What is your relationship status?"

  @BR-D9-03 @BR-D9-04 @BR-D9-08
  Scenario Outline: A single applicant sees their monthly estimate
    Given I live in BC with an eligible status
    And I am "<relationship>" with <dependants> dependent children
    And I <pwd> to apply for the PWD designation
    And my monthly income is $<income>
    When I ask for an estimate
    Then I may be eligible for "<amount>" per month
    And my information shows a family size of <size> and a "<household>" household

    Examples: Dependants raise the family size; PWD changes the client type
      | relationship             | dependants | pwd         | income | amount    | size | household     |
      | Single and Never Married | 0          | do not plan | 0      | $1,060.00 | 1    | Single        |
      | Single and Never Married | 0          | plan        | 0      | $1,535.50 | 1    | Single        |
      | Divorced                 | 2          | do not plan | 300    | $1,200.00 | 3    | Single parent |
      | Widowed                  | 1          | plan        | 500    | $1,380.50 | 2    | Single parent |

  @BR-D9-04 @BR-D9-08
  Scenario Outline: A couple's estimate counts both incomes and both PWD answers
    Given I live in BC with an eligible status
    And I am "<relationship>" with 0 dependent children
    And I <pwd> to apply for the PWD designation
    And my spouse <spousePwd> to apply for the PWD designation
    And my monthly income is $400
    And my spouse's monthly income is $250
    When I ask for an estimate
    Then I may be eligible for "<amount>" per month
    And my information shows a family size of 2 and a "<household>" household
    And my information shows a monthly income of "$650" and assets of "$0"

    Examples:
      | relationship               | pwd         | spousePwd     | amount    | household     |
      | Married                    | do not plan | does not plan | $1,000.00 | Married       |
      | Marriage-Like Relationship | plan        | does not plan | $1,640.50 | Marriage-like |
      | Married                    | plan        | plans         | $2,116.00 | Married       |

  @BR-D9-08
  Scenario: Income at or above the limit gives a $0 estimate
    Given I live in BC with an eligible status
    And I am "Single and Never Married" with 0 dependent children
    And I do not plan to apply for the PWD designation
    And my monthly income is $1200
    When I ask for an estimate
    Then I may not be eligible, with an estimate of $0
    And I am told why my estimate is $0

  @BR-D9-06 @BR-D9-07
  Scenario: Assets over the ceiling disqualify regardless of income
    Given I live in BC with an eligible status
    And I am "Single and Never Married" with 0 dependent children
    And I do not plan to apply for the PWD designation
    And my other assets are worth $6000
    When I ask for an estimate
    Then I may not be eligible, with an estimate of $0
    And my information shows a monthly income of "$0" and assets of "$6,000"

  @BR-D9-06
  Scenario: A dependant raises the asset ceiling
    Given I live in BC with an eligible status
    And I am "Single and Never Married" with 1 dependent children
    And I do not plan to apply for the PWD designation
    And my other assets are worth $6000
    When I ask for an estimate
    Then I may be eligible for "$1,405.00" per month

  @a11y
  Scenario: Unanswered questions block the estimate and take focus
    Given I live in BC with an eligible status
    When I ask for an estimate
    Then I am asked to answer the questions marked as required
    And focus has moved into the form
